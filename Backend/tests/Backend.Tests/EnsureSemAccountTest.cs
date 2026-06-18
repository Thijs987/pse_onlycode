using System;
using System.IO;
using System.Threading.Tasks;
using Application.Services;
using Application;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Tests
{
    /// <summary>
    /// Ensure there is exactly one account for semvdberge@gmail.com.
    /// This test is intentionally persistent: it will create the account if missing and will NOT remove it.
    /// It will be a no-op when no integration connection string is configured.
    /// </summary>
    public class EnsureSemAccountTest
    {
        private static string? IntegrationConnectionString =>
            Environment.GetEnvironmentVariable("ConnectionStrings__Integration")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        private static bool HasIntegrationConnection() => !string.IsNullOrWhiteSpace(IntegrationConnectionString);

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(IntegrationConnectionString!)
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task Ensure_Semvdberge_Account_Exists()
        {
            if (!HasIntegrationConnection())
            {
                // No integration DB configured; skip but pass the test so CI is not blocked.
                Assert.True(true, "Integration DB not configured; skipped persistent account creation.");
                return;
            }

            var email = "semvdberge@gmail.com";

            await using var db = CreateContext();

            var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email || u.Username == "semvdberge");
            if (existing != null)
            {
                // Already present or username already in use — nothing to do. Keep the account.
                Assert.True(true);
                return;
            }

            // Load email settings if present (prefer env vars, then appsettings)
            EmailSettings? emailSettings = null;
            var smtpUser = Environment.GetEnvironmentVariable("EMAIL_SMTP_USERNAME");
            var smtpPass = Environment.GetEnvironmentVariable("EMAIL_SMTP_PASSWORD");
            if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
            {
                emailSettings = new EmailSettings
                {
                    Host = Environment.GetEnvironmentVariable("EMAIL_SMTP_HOST") ?? "smtp.gmail.com",
                    Port = int.TryParse(Environment.GetEnvironmentVariable("EMAIL_SMTP_PORT"), out var p) ? p : 587,
                    Username = smtpUser,
                    Password = smtpPass,
                    EnableSsl = true,
                    FromEmail = smtpUser
                };
            }

            if (emailSettings == null)
            {
                // Try appsettings.json in repository (convenience for local dev)
                try
                {
                    var candidate = Path.Combine("/home/sem/PSE-Green-Code","Backend","src","appsettings.json");
                    if (System.IO.File.Exists(candidate))
                    {
                        var config = new ConfigurationBuilder().AddJsonFile(candidate, optional: false).Build();
                        var section = config.GetSection("EmailSettings");
                        var loaded = new EmailSettings();
                        section.Bind(loaded);
                        if (!string.IsNullOrWhiteSpace(loaded.Username) && !string.IsNullOrWhiteSpace(loaded.Password))
                        {
                            emailSettings = loaded;
                        }
                    }
                }
                catch { /* ignore */ }
            }

            IEmailService emailService;
            if (emailSettings != null)
            {
                var options = Options.Create(emailSettings!);
                emailService = new GmailEmailService(options);
            }
            else
            {
                emailService = new ConsoleEmailService();
            }

            var service = new AuthService(
                db,
                new InMemoryAuditService(),
                new InMemoryRateLimitService(),
                emailService);

            // Create the account; choose a username that's unlikely to collide
            var username = "semvdberge";
            var registerResult = await service.Register(email, username, "Password1!", "127.0.0.1");
            Assert.True(registerResult.IsSuccess, registerResult.Error?.Message ?? "Register failed");

            // Do not delete — test intentionally persists the account.
            Assert.True(true);
        }
    }
}
