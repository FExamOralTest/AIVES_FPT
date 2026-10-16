using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    /// <summary>
    /// Tự động seed 1 admin mặc định khi app khởi động.
    /// Idempotent — bỏ qua nếu username đã tồn tại.
    /// Credentials được đọc từ configuration.
    /// </summary>
    public static async Task SeedAdminAsync(AivesDbContext db, IConfiguration configuration, ILogger logger)
    {
        var adminUsername = configuration["Admin:Username"] ?? "admin";
        var adminEmail = configuration["Admin:Email"] ?? "admin@aives.edu.vn";
        var adminPassword = configuration["Admin:Password"];

        // Don't seed with default weak password in production
        if (string.IsNullOrEmpty(adminPassword))
        {
            var isProduction = !Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?.Equals("Development", StringComparison.OrdinalIgnoreCase) ?? false;

            if (isProduction)
            {
                logger.LogWarning(
                    "Admin account will not be auto-seeded in production. " +
                    "Please set Admin:Username, Admin:Email, and Admin:Password configuration values.");
                return;
            }

            // Use a secure default for development only
            adminPassword = "Dev@12345";
            logger.LogInformation("Using default development admin password. Set Admin:Password in configuration for production.");
        }

        var exists = await db.Users.AnyAsync(u => u.Username == adminUsername);
        if (exists)
        {
            logger.LogDebug("Admin user already exists, skipping seed.");
            return;
        }

        var admin = new User
        {
            Username = adminUsername,
            FullName = "System Admin",
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 12),
            Role = UserRole.ADMIN,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();

        logger.LogInformation("Admin user '{Username}' seeded successfully", adminUsername);

        // In Development mode, seed test users for all roles if they don't exist
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?.Equals("Development", StringComparison.OrdinalIgnoreCase) ?? true;

        if (isDev)
        {
            var testUsers = new[]
            {
                (Username: "staff_01", FullName: "Nguyen Van Staff (Giang vien)", Email: "staff01@aives.edu.vn", Role: UserRole.STAFF),
                (Username: "examiner_01", FullName: "Tran Thi Examiner (Khao thi)", Email: "examiner01@aives.edu.vn", Role: UserRole.EXAMINER),
                (Username: "student_01", FullName: "Le Van Student (Sinh vien)", Email: "student01@aives.edu.vn", Role: UserRole.STUDENT),
            };

            foreach (var (username, fullName, email, role) in testUsers)
            {
                if (!await db.Users.AnyAsync(u => u.Username == username))
                {
                    db.Users.Add(new User
                    {
                        Username = username,
                        FullName = fullName,
                        Email = email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Dev@12345", workFactor: 12),
                        Role = role,
                        CreatedAt = DateTime.UtcNow
                    });
                    logger.LogInformation("Test user '{Username}' ({Role}) seeded successfully", username, role);
                }
            }
            await db.SaveChangesAsync();
        }
    }
}

