using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Infrastructure.Repositories;
using Aives.Application.Services;
using Aives.Application.Auth.Interfaces;
using Aives.Application.Auth.Services;
using Aives.Infrastructure.Auth;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.Services;
using Aives.Application.Viva.Interfaces;
using Aives.Application.Viva.Services;
using Aives.Infrastructure.AI;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<UserRole>(         "user_role",         new NpgsqlUpperCaseTranslator());
dataSourceBuilder.MapEnum<ExamStatus>(       "exam_status",       new NpgsqlUpperCaseTranslator());
dataSourceBuilder.MapEnum<SessionStatus>(    "session_status",    new NpgsqlUpperCaseTranslator());
dataSourceBuilder.MapEnum<SubmissionStatus>( "submission_status", new NpgsqlUpperCaseTranslator());
dataSourceBuilder.MapEnum<DocType>(          "doc_type",          new NpgsqlUpperCaseTranslator());
var dataSource = dataSourceBuilder.Build();

if (builder.Configuration.GetValue<bool>("Docker:InitializeDatabase"))
{
    var database = new NpgsqlConnectionStringBuilder(connectionString);
    if (!builder.Environment.IsDevelopment() || database.Host != "postgres" || database.Database != "aives_dev")
        throw new InvalidOperationException("Docker database initialization is restricted to the local development database.");

    // Existing migrations require a pre-existing schema; bootstrap only the isolated Docker database.
    var options = new DbContextOptionsBuilder<AivesDbContext>().UseNpgsql(dataSource).Options;
    await using var db = new AivesDbContext(options);
    await db.Database.EnsureCreatedAsync();
    return;
}

builder.Services.AddDbContext<AivesDbContext>(options =>
    options.UseNpgsql(dataSource));

var jwtKey      = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key configuration is required.");
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "AIVES";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AIVES.Client";
builder.Services.AddSingleton<IJwtBlacklistService, JwtBlacklistService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var blacklistService = context.HttpContext.RequestServices.GetRequiredService<IJwtBlacklistService>();
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var rawToken = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                    ? authHeader["Bearer ".Length..].Trim()
                    : null;

                if (!string.IsNullOrEmpty(rawToken) && await blacklistService.IsTokenBlacklistedAsync(rawToken))
                {
                    context.Fail("Token has been revoked.");
                    return;
                }
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var users = context.HttpContext.RequestServices.GetRequiredService<Aives.Application.Auth.Interfaces.IUserRepository>();
                var currentUser = int.TryParse(userId, out var id) ? await users.GetByIdAsync(id) : null;
                if (currentUser is null || context.Principal?.FindFirstValue(ClaimTypes.Role) != currentUser.Role.ToString())
                    context.Fail("The account no longer exists or its role has changed.");
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("LoginPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

builder.Services.AddScoped<IAuthService,    AuthService>();
builder.Services.AddScoped<IJwtService,     JwtService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<Aives.Application.Auth.Interfaces.IUserRepository,
                            Aives.Infrastructure.Auth.UserRepository>();

builder.Services.AddScoped<IExamRepository, ExamRepository>();
builder.Services.AddScoped<IExamSessionRepository, ExamSessionRepository>();
builder.Services.AddScoped<IQuestionBankRepository, QuestionBankRepository>();
builder.Services.AddScoped<IQuestionBankService, QuestionBankService>();
builder.Services.AddScoped<IExamUnitOfWork, ExamUnitOfWork>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IExamSessionService, ExamSessionService>();

builder.Services.AddScoped<IMarkdownDocumentRepository, MarkdownDocumentRepository>();
builder.Services.AddScoped<Aives.Application.Interfaces.Repositories.IUserRepository, Aives.Infrastructure.Repositories.UserRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IResultRepository, ResultRepository>();
builder.Services.AddScoped<IUserService,             UserService>();
builder.Services.AddScoped<ICourseService,           CourseService>();
builder.Services.AddScoped<IMarkdownDocumentService, MarkdownDocumentService>();
builder.Services.AddScoped<IExamGeneratorService,   ExamGeneratorService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IResultService, ResultService>();
builder.Services.AddScoped<IVivaSessionService, VivaSessionService>();
builder.Services.AddHttpClient<IAIQuestionService, AIQuestionService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI:BaseUrl"] ?? "http://localhost:8000");
    var timeoutSeconds = builder.Configuration.GetValue<double>("AI:QuestionGenerationTimeoutSeconds", 300);
    if (!double.IsFinite(timeoutSeconds) || timeoutSeconds <= 0 || timeoutSeconds > 3600)
        throw new InvalidOperationException("AI:QuestionGenerationTimeoutSeconds must be between 0 and 3600.");
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddHttpClient<Aives.Application.AI.Interfaces.IAiServiceClient, AiServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddScoped<Aives.Infrastructure.AI.IAiServiceClient>(sp =>
    (AiServiceClient)sp.GetRequiredService<Aives.Application.AI.Interfaces.IAiServiceClient>());

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AIVES API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Description  = "Enter: Bearer {your JWT token}",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        Reference    = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id   = JwtBearerDefaults.AuthenticationScheme
        }
    };
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DatabaseSeeder.SeedAdminAsync(db, configuration, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseCors("CorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/health/db", async (AivesDbContext db) =>
{
    var canConnect  = await db.Database.CanConnectAsync();
    var userCount   = await db.Users.CountAsync();
    var courseCount = await db.Courses.CountAsync();
    return Results.Ok(new
    {
        Status            = "Healthy",
        DatabaseConnected = canConnect,
        UserCount         = userCount,
        CourseCount       = courseCount,
        ServerTime        = DateTime.UtcNow
    });
});

app.Run();

