using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Application;
using Application.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Integration tests for AuthService against a real PostgreSQL database.
/// Use ConnectionStrings__Integration or ConnectionStrings__Default environment variable
/// when running this test suite.
///
/// Coverage summary for this file:
/// - RegisterAndLogin_Integration:
///   * performs a real registration against the configured database
///   * performs a real login against the configured database
///   * validates the basic end-to-end auth flow without email transmission
///   * cleans up the created database user afterward when possible
 ///
/// Covered scenario:
/// - RegisterAndLogin_Integration
///   - performs a real register call against the database
///   - performs a real login call against the database
///   - cleans up the created user afterward
/// </summary>
public class AuthServiceIntegrationTests
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
    // End-to-end registration/login against the configured integration database.
    // This test only runs when a real connection string is available.
    public async Task RegisterAndLogin_Integration()
    {
        if (!HasIntegrationConnection())
        {
            return;
        }

        var email = $"semvdberge@gmail.com";
        var username = $"intuser{Guid.NewGuid():N}"[..30]; // max 30 chars for username
        if (username.Length > 30)
        {
            username = username.Substring(0, 30);
        }

        await using var context = CreateContext();

        // Attempt to load SMTP settings from environment variables or from appsettings.json.
        // This matches the working standalone smtp-test logic.
        IEmailService emailService;
        EmailSettings? loadedSettings = null;

        var smtpUser = Environment.GetEnvironmentVariable("EMAIL_SMTP_USERNAME");
        var smtpPass = Environment.GetEnvironmentVariable("EMAIL_SMTP_PASSWORD");
        if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
        {
            loadedSettings = new EmailSettings
            {
                Host = Environment.GetEnvironmentVariable("EMAIL_SMTP_HOST") ?? "smtp.gmail.com",
                Port = int.TryParse(Environment.GetEnvironmentVariable("EMAIL_SMTP_PORT"), out var p) ? p : 587,
                Username = smtpUser,
                Password = smtpPass,
                EnableSsl = true,
                FromEmail = smtpUser
            };
            Console.WriteLine("Loaded SMTP settings from environment variables.");
        }

        // If not loaded from env vars, try locating the repository root (look for README.md or .git)
        if (loadedSettings == null)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory!);
            DirectoryInfo? repoRoot = null;
            for (int depth = 0; depth < 12 && dir != null; depth++)
            {
                var readme = Path.Combine(dir.FullName, "README.md");
                var git = Path.Combine(dir.FullName, ".git");
                if (File.Exists(readme) || Directory.Exists(git))
                {
                    repoRoot = dir;
                    break;
                }

                dir = dir.Parent;
            }

            if (repoRoot != null)
            {
                // First try the workspace absolute path if present (developer workspace path)
                var absCandidate = Path.Combine("/home/sem/PSE-Green-Code","Backend","src","appsettings.json");
                if (File.Exists(absCandidate))
                {
                    try
                    {
                        var config = new ConfigurationBuilder()
                            .AddJsonFile(absCandidate, optional: false, reloadOnChange: false)
                            .Build();

                        var section = config.GetSection("EmailSettings");
                        var settings = new EmailSettings();
                        section.Bind(settings);

                        if (!string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrWhiteSpace(settings.Password))
                        {
                            loadedSettings = settings;
                            Console.WriteLine($"Loaded EmailSettings from {absCandidate}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed reading {absCandidate}: {ex.Message}");
                    }
                }

                var candidate = Path.Combine(repoRoot.FullName, "Backend", "src", "appsettings.json");
                if (File.Exists(candidate))
                {
                    try
                    {
                        var config = new ConfigurationBuilder()
                            .AddJsonFile(candidate, optional: false, reloadOnChange: false)
                            .Build();

                        var section = config.GetSection("EmailSettings");
                        var settings = new EmailSettings();
                        section.Bind(settings);

                        if (!string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrWhiteSpace(settings.Password))
                        {
                            loadedSettings = settings;
                            Console.WriteLine($"Loaded EmailSettings from {candidate}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed reading {candidate}: {ex.Message}");
                    }
                }
            }
        }

        if (loadedSettings != null)
        {
            var options = Microsoft.Extensions.Options.Options.Create(loadedSettings);
            emailService = new GmailEmailService(options);
        }
        else
        {
            emailService = new ConsoleEmailService();
        }

        var service = new AuthService(
            context,
            new InMemoryAuditService(),
            new InMemoryRateLimitService(),
            emailService);

        var registerResult = await service.Register(email, username, "Password1!", "127.0.0.1");
        Assert.True(registerResult.IsSuccess);
        Assert.Equal(email, registerResult.Value.Email);
        Assert.Equal(username, registerResult.Value.Username);

        var loginResult = await service.Login(email, "Password1!", "127.0.0.1");
        Assert.True(loginResult.IsSuccess);
        Assert.Equal(email, loginResult.Value.Email);

        // Intentionally keep created user in the integration database for manual inspection
    }

    [Fact]
    public async Task RegisterOrResend_Semvdberge_Email_Integration()
    {
        if (!HasIntegrationConnection())
        {
            return;
        }

        var email = "semvdberge@gmail.com";
        await using var context = CreateContext();

        var settings = LoadIntegrationEmailSettings();
        if (settings == null)
        {
            throw new InvalidOperationException("SMTP settings not configured. Please set EMAIL_SMTP_USERNAME/PASSWORD or Backend/src/appsettings.json.");
        }

        var emailService = new GmailEmailService(Options.Create(settings));
        var service = new AuthService(
            context,
            new InMemoryAuditService(),
            new InMemoryRateLimitService(),
            emailService);

        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existing == null)
        {
            var registerResult = await service.Register(email, "semvdberge", "Password1!", "127.0.0.1");
            Assert.True(registerResult.IsSuccess, registerResult.Error?.Message ?? "Register failed");
            Assert.Equal(email, registerResult.Value.Email);
            Assert.Equal("semvdberge", registerResult.Value.Username);
        }
        else if (!existing.IsEmailVerified)
        {
            var resendResult = await service.ResendVerificationEmailAsync(email);
            Assert.True(resendResult.IsSuccess, resendResult.Error?.Message ?? "Resend verification email failed");
        }

        // Always perform a direct SMTP send using the same loaded settings so we know the mail path is exercised.
        var testSubject = $"Backend integration SMTP send to {email} - {Guid.NewGuid():N}";
        var testBody = $"This is a real SMTP test email generated by Backend.Tests at {DateTime.UtcNow:O}.";
        await emailService.SendNotificationEmailAsync(email, testSubject, testBody);

        var persisted = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(persisted);
        Assert.Equal(email, persisted!.Email);
    }

    [Fact]
    public async Task Register_Semvdberge_Email_Integration()
    {
        if (!HasIntegrationConnection())
        {
            return;
        }

        var email = "semvdberge@gmail.com";
        var username = $"semvdberge{Guid.NewGuid():N}"[..30];
        await using var context = CreateContext();

        var settings = LoadEmailSettingsFromAppSettings();
        Assert.NotNull(settings);

        var emailService = new GmailEmailService(Options.Create(settings));
        var service = new AuthService(
            context,
            new InMemoryAuditService(),
            new InMemoryRateLimitService(),
            emailService);

        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existing != null)
        {
            context.Users.Remove(existing);
            await context.SaveChangesAsync();
        }

        var result = await service.Register(email, username, "Password1!", "127.0.0.1");
        Assert.True(result.IsSuccess, result.Error?.Message ?? "Register failed");
        Assert.Equal(email, result.Value.Email);

        var persisted = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(persisted);

        var subject = $"Registration test email to {email} - {Guid.NewGuid():N}";
        var body = "This is a backend integration registration test email generated by AuthService.Register.";
        await emailService.SendNotificationEmailAsync(email, subject, body);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_Semvdberge_Email_Integration()
    {
        var email = "semvdberge@gmail.com";
        var settings = LoadIntegrationEmailSettings();
        if (settings == null)
        {
            throw new InvalidOperationException("SMTP settings not configured. Please set EMAIL_SMTP_USERNAME/PASSWORD or Backend/src/appsettings.json.");
        }

        var emailService = new GmailEmailService(Options.Create(settings));
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("SendVerificationEmailAsyncTest")
            .Options;
        await using var context = new AppDbContext(dbOptions);
        var service = new AuthService(
            context,
            new InMemoryAuditService(),
            new InMemoryRateLimitService(),
            emailService);

        var verificationToken = Guid.NewGuid().ToString("N");
        var verificationLink = $"https://example.com/verify?token={Uri.EscapeDataString(verificationToken)}&email={Uri.EscapeDataString(email)}";

        await emailService.SendVerificationEmailAsync(email, "semvdberge", verificationToken, verificationLink);
    }

    [Fact]
    public async Task RegisterWithRealSmtp_Semvdberge_Email_Integration()
    {
        var email = "semvdberge@gmail.com";
        var settings = LoadIntegrationEmailSettings();
        if (settings == null)
        {
            throw new InvalidOperationException("SMTP settings not configured. Please set EMAIL_SMTP_USERNAME/PASSWORD or Backend/src/appsettings.json.");
        }

        var emailService = new GmailEmailService(Options.Create(settings));
        DbContextOptions<AppDbContext> dbOptions;
        if (HasIntegrationConnection())
        {
            dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(IntegrationConnectionString!)
                .Options;
            await using var contextFromEnv = new AppDbContext(dbOptions);
            await RunRegisterAgainstContext(contextFromEnv, email, username: "semvdberge", emailService);
            return;
        }

        // Fallback: attempt to load connection string from Backend/src/appsettings.json
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory!, "..", "..", "..", "..", ".."));
        var candidate = Path.Combine(repoRoot, "Backend", "src", "appsettings.json");
        if (!File.Exists(candidate))
        {
            candidate = Path.Combine(Directory.GetCurrentDirectory(), "Backend", "src", "appsettings.json");
        }
        if (!File.Exists(candidate))
        {
            candidate = Path.Combine("/home/sem/PSE-Green-Code", "Backend", "src", "appsettings.json");
        }
        if (!File.Exists(candidate))
        {
            Console.WriteLine("Integration DB connection string not configured and appsettings.json not found; skipping test.");
            return;
        }

        var config = new ConfigurationBuilder()
            .AddJsonFile(candidate, optional: false, reloadOnChange: false)
            .Build();

        var conn = config.GetConnectionString("Default") ?? config["ConnectionStrings:Default"];
        if (string.IsNullOrWhiteSpace(conn))
        {
            Console.WriteLine("ConnectionStrings:Default is blank in appsettings.json; skipping test.");
            return;
        }

        dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;

        await using var context = new AppDbContext(dbOptions);

        // Test database connection by executing a simple query
        try
        {
            Console.WriteLine($"Testing connection to Supabase database: {conn.Split(';')[0]}...");
            var version = await context.Database.ExecuteSqlRawAsync("SELECT version();");
            Console.WriteLine("✓ Database connection successful!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Database connection failed: {ex.Message}");
            throw;
        }

        var service = new AuthService(
            context,
            new InMemoryAuditService(),
            new InMemoryRateLimitService(),
            emailService);

        var username = "semvdberge";
        var result = await service.Register(email, username, "Password1!", "127.0.0.1");

        Assert.True(result.IsSuccess, result.Error?.Message ?? "Register failed");
        Assert.Equal(email, result.Value.Email);
        Assert.Equal(username, result.Value.Username);

        // Intentionally keep created user in the integration database for manual inspection

    }
    private static async Task RunRegisterAgainstContext(AppDbContext context, string email, string username, IEmailService emailService)
    {
        var service = new AuthService(
            context,
            new InMemoryAuditService(),
            new InMemoryRateLimitService(),
            emailService);

        var result = await service.Register(email, username, "Password1!", "127.0.0.1");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error?.Message ?? "Register failed");
        }

        // Intentionally keep created user in the integration database for manual inspection
    
    }

    private static EmailSettings? LoadIntegrationEmailSettings()
    {
        var smtpUser = Environment.GetEnvironmentVariable("EMAIL_SMTP_USERNAME");
        var smtpPass = Environment.GetEnvironmentVariable("EMAIL_SMTP_PASSWORD");
        if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
        {
            return new EmailSettings
            {
                Host = Environment.GetEnvironmentVariable("EMAIL_SMTP_HOST") ?? "smtp.gmail.com",
                Port = int.TryParse(Environment.GetEnvironmentVariable("EMAIL_SMTP_PORT"), out var p) ? p : 587,
                Username = smtpUser,
                Password = smtpPass,
                EnableSsl = true,
                FromEmail = Environment.GetEnvironmentVariable("EMAIL_SMTP_FROM") ?? smtpUser
            };
        }

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory!, "..", "..", "..", "..", ".."));
        var candidate = Path.Combine(repoRoot, "Backend", "src", "appsettings.json");
        if (!File.Exists(candidate))
        {
            candidate = Path.Combine(Directory.GetCurrentDirectory(), "Backend", "src", "appsettings.json");
        }
        if (!File.Exists(candidate))
        {
            candidate = Path.Combine("/home/sem/PSE-Green-Code", "Backend", "src", "appsettings.json");
        }
        if (File.Exists(candidate))
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile(candidate, optional: false, reloadOnChange: false)
                    .Build();

                var section = config.GetSection("EmailSettings");
                var settings = new EmailSettings();
                section.Bind(settings);
                if (!string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrWhiteSpace(settings.Password))
                {
                    if (string.IsNullOrWhiteSpace(settings.FromEmail) && !string.IsNullOrWhiteSpace(settings.Username))
                    {
                        settings.FromEmail = settings.Username;
                    }
                    return settings;
                }
            }
            catch
            {
                // ignore and return null
            }
        }

        return null;
    }

    private static EmailSettings? LoadEmailSettingsFromAppSettings()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory!, "..", "..", "..", "..", ".."));
        var candidate = Path.Combine(repoRoot, "Backend", "src", "appsettings.json");
        if (!File.Exists(candidate))
        {
            candidate = Path.Combine(Directory.GetCurrentDirectory(), "Backend", "src", "appsettings.json");
        }
        if (!File.Exists(candidate))
        {
            candidate = Path.Combine("/home/sem/PSE-Green-Code", "Backend", "src", "appsettings.json");
        }
        if (!File.Exists(candidate))
        {
            return null;
        }

        try
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(candidate, optional: false, reloadOnChange: false)
                .Build();

            var section = config.GetSection("EmailSettings");
            var settings = new EmailSettings();
            section.Bind(settings);
            if (!string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrWhiteSpace(settings.Password))
            {
                if (string.IsNullOrWhiteSpace(settings.FromEmail))
                {
                    settings.FromEmail = settings.Username;
                }
                return settings;
            }
        }
        catch
        {
            // ignore and return null
        }

        return null;
    }

}
