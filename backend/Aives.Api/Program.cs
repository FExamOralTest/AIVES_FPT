using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.AI;

// Auth
using Aives.Application.Auth.Interfaces;
using Aives.Application.Auth.Services;
using Aives.Infrastructure.Auth;

// Courses
using Aives.Application.Courses.Interfaces;
using Aives.Application.Courses.Services;
using Aives.Infrastructure.Repositories.Courses;

// Documents
using Aives.Application.Documents.Interfaces;
using Aives.Application.Documents.Services;
using Aives.Infrastructure.Repositories.Documents;

// Exams
using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.Services;
using Aives.Infrastructure.Repositories.Exams;

// Questions
using Aives.Application.Questions.Interfaces;
using Aives.Application.Questions.Services;
using Aives.Infrastructure.Repositories.Questions;

// Results
using Aives.Application.Results.Interfaces;
using Aives.Application.Results.Services;
using Aives.Infrastructure.Repositories.Results;

// Users
using Aives.Application.Users.Interfaces;
using Aives.Application.Users.Services;
using Aives.Infrastructure.Repositories.Users;

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

builder.Services.AddDbContext<AivesDbContext>(options => options.UseNpgsql(dataSource));

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
// 3. DI — Auth
// ──────────────────────────────────────────────
builder.Services.AddScoped<IAuthService,    AuthService>();
builder.Services.AddScoped<IJwtService,     JwtService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<Aives.Application.Auth.Interfaces.IUserRepository,
                            Aives.Infrastructure.Auth.UserRepository>();

// ──────────────────────────────────────────────
// 4. DI — Courses
// ──────────────────────────────────────────────
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ICourseService,    CourseService>();

// ──────────────────────────────────────────────
// 5. DI — Documents
// ──────────────────────────────────────────────
builder.Services.AddScoped<IMarkdownDocumentRepository, MarkdownDocumentRepository>();
builder.Services.AddScoped<IMarkdownDocumentService,    MarkdownDocumentService>();
builder.Services.AddScoped<IDocumentService,            DocumentService>();

// ──────────────────────────────────────────────
// 6. DI — Exams
// ──────────────────────────────────────────────
builder.Services.AddScoped<IExamRepository,        ExamRepository>();
builder.Services.AddScoped<IExamSessionRepository, ExamSessionRepository>();
builder.Services.AddScoped<IExamService,           ExamService>();
builder.Services.AddScoped<IExamSessionService,    ExamSessionService>();

// ──────────────────────────────────────────────
// 7. DI — Questions
// ──────────────────────────────────────────────
builder.Services.AddScoped<IQuestionBankRepository, QuestionBankRepository>();
builder.Services.AddScoped<IExamGeneratorService,   ExamGeneratorService>();

// ──────────────────────────────────────────────
// 8. DI — Results
// ──────────────────────────────────────────────
builder.Services.AddScoped<IResultRepository,   ResultRepository>();
builder.Services.AddScoped<IResultService,      ResultService>();
builder.Services.AddScoped<IExamHistoryService, ExamHistoryService>();

// ──────────────────────────────────────────────
// 9. DI — Users
// ──────────────────────────────────────────────
builder.Services.AddScoped<Aives.Application.Users.Interfaces.IUserRepository,
                            Aives.Infrastructure.Repositories.Users.UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

// ──────────────────────────────────────────────
// 10. DI — AI (HttpClient)
// ──────────────────────────────────────────────
builder.Services.AddHttpClient<IAIQuestionService, AIQuestionService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout     = TimeSpan.FromMinutes(5);
});

// ──────────────────────────────────────────────
// 11. Controllers + Swagger with JWT
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
// 12. Auto-seed on startup
// ──────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
    await DatabaseSeeder.SeedAdminAsync(db);
    await DatabaseSeeder.SeedQuestionBankAsync(db);
    await DatabaseSeeder.SeedExamDataAsync(db);
}

// ──────────────────────────────────────────────
// 13. Middleware pipeline
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
    Results.Ok(new
    {
        Status            = "Healthy",
        DatabaseConnected = await db.Database.CanConnectAsync(),
        UserCount         = await db.Users.CountAsync(),
        CourseCount       = await db.Courses.CountAsync(),
        ServerTime        = DateTime.UtcNow
    }));

app.Run();
