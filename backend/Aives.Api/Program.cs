using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
using Aives.Infrastructure.AI;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// 1. PostgreSQL / EF Core
// ──────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<UserRole>(         "user_role",         new NpgsqlUpperCaseTranslator());
dataSourceBuilder.MapEnum<ExamStatus>(       "exam_status",       new NpgsqlUpperCaseTranslator());
dataSourceBuilder.MapEnum<SessionStatus>(    "session_status",    new NpgsqlUpperCaseTranslator());
dataSourceBuilder.MapEnum<SubmissionStatus>( "submission_status", new NpgsqlUpperCaseTranslator());
dataSourceBuilder.MapEnum<DocType>(          "doc_type",          new NpgsqlUpperCaseTranslator());
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AivesDbContext>(options =>
    options.UseNpgsql(dataSource));

// ──────────────────────────────────────────────
// 2. JWT Authentication
// ──────────────────────────────────────────────
var jwtKey      = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key configuration is required.");
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "AIVES";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AIVES.Client";

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
    });

builder.Services.AddAuthorization();

// ──────────────────────────────────────────────
// 3. Dependency Injection — Auth
// ──────────────────────────────────────────────
builder.Services.AddScoped<IAuthService,    AuthService>();
builder.Services.AddScoped<IJwtService,     JwtService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<Aives.Application.Auth.Interfaces.IUserRepository,
                            Aives.Infrastructure.Auth.UserRepository>();

// ──────────────────────────────────────────────
// 4. Dependency Injection — Repositories
// ──────────────────────────────────────────────
builder.Services.AddScoped<Aives.Application.Interfaces.Repositories.IUserRepository,
                            Aives.Infrastructure.Repositories.UserRepository>();
builder.Services.AddScoped<ICourseRepository,           CourseRepository>();
builder.Services.AddScoped<IMarkdownDocumentRepository, MarkdownDocumentRepository>();
builder.Services.AddScoped<IExamRepository,             ExamRepository>();
builder.Services.AddScoped<IExamSessionRepository,      ExamSessionRepository>();
builder.Services.AddScoped<IQuestionBankRepository,     QuestionBankRepository>();
builder.Services.AddScoped<IResultRepository,           ResultRepository>();

// ──────────────────────────────────────────────
// 5. Dependency Injection — Application Services
// ──────────────────────────────────────────────
builder.Services.AddScoped<IUserService,            UserService>();
builder.Services.AddScoped<ICourseService,          CourseService>();
builder.Services.AddScoped<IDocumentService,        DocumentService>();
builder.Services.AddScoped<IMarkdownDocumentService, MarkdownDocumentService>();
builder.Services.AddScoped<IExamService,            ExamService>();
builder.Services.AddScoped<IExamSessionService,     ExamSessionService>();
builder.Services.AddScoped<IExamHistoryService,     ExamHistoryService>();
builder.Services.AddScoped<IExamGeneratorService,   ExamGeneratorService>();
builder.Services.AddScoped<IResultService,          ResultService>();

// HttpClient cho AI Service (FastAPI)
builder.Services.AddHttpClient<IAIQuestionService, AIQuestionService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromMinutes(5);
});

// ──────────────────────────────────────────────
// 6. Controllers + Swagger with JWT
// ──────────────────────────────────────────────
builder.Services.AddControllers();
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

// ──────────────────────────────────────────────
// 7. Auto-seed on startup
// ──────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
    await DatabaseSeeder.SeedAdminAsync(db);
    await DatabaseSeeder.SeedQuestionBankAsync(db);
    await DatabaseSeeder.SeedExamDataAsync(db);
}

// ──────────────────────────────────────────────
// 8. Middleware pipeline
// ──────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
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