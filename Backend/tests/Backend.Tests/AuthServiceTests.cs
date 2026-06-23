using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Application.Services;
using Application;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Unit tests for Application.AuthService.
/// These tests exercise registration and login business logic using an in-memory database,
/// verifying validation, duplicate checks, rate limiting, lockout and email-verification behavior.
///
/// Coverage summary for this file:
/// - Register path:
///   * valid registration succeeds
///   * missing email / username / password inputs fail with InvalidInput
///   * invalid email formats fail, including whitespace and duplicate @
///   * email is trimmed and normalized before persistence
///   * duplicate email detection is case-insensitive
///   * duplicate username detection is case-insensitive
///   * username length boundaries are enforced (3..30)
///   * password complexity rules are enforced
///   * registration rate limiting is enforced per IP
///   * registration rate limiting also works when IP is null
///   * DB duplicate exception handling returns DuplicateEmail
///   * generic DB errors during register return InternalError
/// - Login path:
///   * valid login succeeds
///   * missing email/password inputs fail with InvalidInput
///   * invalid email formats return InvalidCredentials
///   * non-existent user login returns InvalidCredentials with dummy hash timing
///   * wrong password attempts return InvalidCredentials
///   * repeated wrong password attempts lock the account
///   * active lockout returns AccountLocked
///   * expired lockout allows login and resets counters
///   * successful login resets rate limiting state
///   * unverified email returns EmailNotVerified
/// </summary>
///
/// Covered scenarios:
/// - Register_Succeeds_WithValidInput
/// - Register_ReturnsDuplicateEmail_WhenEmailExists
/// - Register_ReturnsDuplicateUsername_WhenUsernameExists
/// - Register_ReturnsInvalidInput_WhenMissingRequiredFields
/// - Register_ReturnsInvalidInput_OnInvalidEmailFormat
/// - Register_ReturnsInvalidInput_OnWeakPassword
/// - Register_ReturnsRateLimited_WhenTooManyAttempts
/// - Login_Succeeds_WithValidCredentials
/// - Login_ReturnsInvalidInput_WhenMissingRequiredFields
/// - Login_ReturnsRateLimited_WhenTooManyAttempts
/// - Login_ReturnsInvalidCredentials_OnWrongPassword
/// - Login_ReturnsAccountLocked_WhenLockoutIsActive
/// - Login_ReturnsInvalidCredentials_WhenEmailNotVerified
public class AuthServiceTests
{
    private static AppDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    private static AuthService CreateAuthService(AppDbContext context)
    {
        return new AuthService(
            context,
            new InMemoryAuditService(),
            new InMemoryRateLimitService(),
            new ConsoleEmailService());
    }

    private static AuthService CreateAuthService(AppDbContext context, IEmailService emailService)
    {
        return new AuthService(
            context,
            new InMemoryAuditService(),
            new InMemoryRateLimitService(),
            emailService);
    }

    // Helper context subclass to simulate a DB constraint failure during SaveChanges
    private class FailingAppDbContext : AppDbContext
    {
        public FailingAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            // simulate unique constraint violation
            var inner = new Exception("duplicate key value violates unique constraint \"Users_email_key\"");
            throw new Microsoft.EntityFrameworkCore.DbUpdateException("Simulated unique constraint", inner);
        }
    }

    private class FailingDbErrorAppDbContext : AppDbContext
    {
        public FailingDbErrorAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            var inner = new Exception("connection timeout or deadlock");
            throw new Microsoft.EntityFrameworkCore.DbUpdateException("Simulated DB error", inner);
        }
    }

    private class RecordingEmailService : IEmailService
    {
        public int VerificationEmailCount { get; private set; }
        public string? LastTo { get; private set; }
        public string? LastUsername { get; private set; }
        public string? LastToken { get; private set; }
        public string? LastLink { get; private set; }

        public Task SendVerificationEmailAsync(string email, string username, string verificationToken, string verificationLink)
        {
            VerificationEmailCount++;
            LastTo = email;
            LastUsername = username;
            LastToken = verificationToken;
            LastLink = verificationLink;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string email, string username, string resetToken, string resetLink)
        {
            return Task.CompletedTask;
        }

        public Task SendNotificationEmailAsync(string email, string subject, string body)
        {
            return Task.CompletedTask;
        }
    }

    private class ThrowingEmailService : IEmailService
    {
        public Task SendVerificationEmailAsync(string email, string username, string verificationToken, string verificationLink)
        {
            throw new InvalidOperationException("Simulated SMTP failure");
        }

        public Task SendPasswordResetEmailAsync(string email, string username, string resetToken, string resetLink)
        {
            throw new InvalidOperationException("Simulated SMTP failure");
        }

        public Task SendNotificationEmailAsync(string email, string subject, string body)
        {
            throw new InvalidOperationException("Simulated SMTP failure");
        }
    }

    [Fact]
    // Register a valid new user and assert the registration succeeds.
    public async Task Register_Succeeds_WithValidInput()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Register("test@example.com", "testuser", "Password1!", "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("test@example.com", result.Value.Email);
        Assert.Equal("testuser", result.Value.Username);
    }

    [Fact]
    // Register a specific semvdberge@gmail.com account in the unit test and verify email send behavior.
    public async Task Register_Succeeds_WithSemvdbergeEmail_AndInvokesVerificationEmail()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var recordingEmail = new RecordingEmailService();
        var service = CreateAuthService(context, recordingEmail);

        var result = await service.Register("semvdberge@gmail.com", "semvdberge", "Password1!", "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal("semvdberge@gmail.com", result.Value.Email);
        Assert.Equal("semvdberge", result.Value.Username);
        Assert.Equal(1, recordingEmail.VerificationEmailCount);
        Assert.Equal("semvdberge@gmail.com", recordingEmail.LastTo);
        Assert.Equal("semvdberge", recordingEmail.LastUsername);
        Assert.False(string.IsNullOrWhiteSpace(recordingEmail.LastToken));
        Assert.False(string.IsNullOrWhiteSpace(recordingEmail.LastLink));
        Assert.Contains(recordingEmail.LastToken!, recordingEmail.LastLink!);
        Assert.Contains("email=semvdberge%40gmail.com", recordingEmail.LastLink!);

        var user = await context.Users.FirstAsync(u => u.Email == "semvdberge@gmail.com");
        Assert.NotNull(user);
        Assert.False(user.IsEmailVerified);
        Assert.False(string.IsNullOrWhiteSpace(user.VerificationToken));
        Assert.NotNull(user.VerificationTokenExpiry);
    }

    [Fact]
    // Verify that the generated verification link can be clicked to verify the user.
    public async Task Register_GeneratedVerificationLink_CanBeClickedToVerify()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var recordingEmail = new RecordingEmailService();
        var service = CreateAuthService(context, recordingEmail);
        var email = "linkclick@example.com";
        var username = "linkclickuser";

        var registerResult = await service.Register(email, username, "Password1!", "127.0.0.1", baseUrl: "http://localhost");

        Assert.True(registerResult.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(recordingEmail.LastLink));

        var uri = new Uri(recordingEmail.LastLink!);
        var queryParts = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var query = queryParts
            .Select(part => part.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => WebUtility.UrlDecode(pair[1]));

        Assert.Equal(email, query["email"]);
        Assert.True(query.ContainsKey("token"));
        Assert.False(string.IsNullOrWhiteSpace(query["token"]));

        var verificationResult = await service.VerifyEmailAsync(query["email"], query["token"]!);
        Assert.True(verificationResult.IsSuccess);

        var user = await context.Users.FirstAsync(u => u.Email == email);
        Assert.True(user.IsEmailVerified);
        Assert.Null(user.VerificationToken);
        Assert.Null(user.VerificationTokenExpiry);
    }

    [Fact]
    // Verify email with a valid token succeeds and clears the token state.
    public async Task VerifyEmailAsync_Succeeds_WithValidToken()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var token = "valid-token";
        var user = new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "verify@example.com",
            Username = "verifyuser",
            PasswordHash = Application.PasswordHasher.Hash("Password1!"),
            IsEmailVerified = false,
            VerificationToken = Application.PasswordHasher.Hash(token),
            VerificationTokenExpiry = DateTime.UtcNow.AddHours(1)
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.VerifyEmailAsync("verify@example.com", "valid-token");

        Assert.True(result.IsSuccess);
        var refreshed = await context.Users.FirstAsync(u => u.Email == "verify@example.com");
        Assert.True(refreshed.IsEmailVerified);
        Assert.Null(refreshed.VerificationToken);
        Assert.Null(refreshed.VerificationTokenExpiry);
    }

    [Fact]
    // Full request + reset password workflow (unit-test style using in-memory DB)
    public async Task RequestAndResetPassword_Workflow()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var email = $"test-reset-{Guid.NewGuid():N}@example.com";
        var username = ($"testuser{Guid.NewGuid():N}").Substring(0, 12);
        var oldPassword = "OldP@ssword1";

        var reg = await service.Register(email, username, oldPassword, "127.0.0.1");
        System.IO.File.AppendAllText("/tmp/pwreset_debug.txt", $"Register: Success={reg.IsSuccess} Error={reg.Error?.Message}\n");
        Assert.True(reg.IsSuccess);

        var req = await service.RequestPasswordResetAsync(email);
        System.IO.File.AppendAllText("/tmp/pwreset_debug.txt", $"Request: Success={req.IsSuccess} ErrorCode={req.Error?.Code} ErrorMsg={req.Error?.Message}\n");
        Assert.True(req.IsSuccess, $"RequestPasswordResetAsync failed: Code={req.Error?.Code} Message={req.Error?.Message}");

        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.NotNull(user!.PasswordResetToken);
        Assert.NotNull(user!.PasswordResetTokenExpiry);

        // For test, set a known token and hash it so we can call ResetPasswordAsync with the plaintext
        var tokenBytes = new byte[48];
        System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        user.PasswordResetToken = Application.PasswordHasher.Hash(token);
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await context.SaveChangesAsync();

        var newPassword = "NewP@ssword1";
        var reset = await service.ResetPasswordAsync(email, token, newPassword);
        System.IO.File.AppendAllText("/tmp/pwreset_debug.txt", $"Reset: Success={reset.IsSuccess} ErrorCode={reset.Error?.Code} ErrorMsg={reset.Error?.Message}\n");
        Assert.True(reset.IsSuccess, $"ResetPasswordAsync failed: Code={reset.Error?.Code} Message={reset.Error?.Message}");

        var updated = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.True(Application.PasswordHasher.Verify(newPassword, updated!.PasswordHash));
    }

    [Fact]
    // Resend verification email updates the token and sends a new email for unverified users.
    public async Task ResendVerificationEmailAsync_Succeeds_ForUnverifiedUser()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var user = new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "resend@example.com",
            Username = "resenduser",
            PasswordHash = Application.PasswordHasher.Hash("Password1!"),
            IsEmailVerified = false,
            VerificationToken = "old-token",
            VerificationTokenExpiry = DateTime.UtcNow.AddHours(-1)
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var recordingEmail = new RecordingEmailService();
        var service = CreateAuthService(context, recordingEmail);
        var result = await service.ResendVerificationEmailAsync("resend@example.com");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, recordingEmail.VerificationEmailCount);
        Assert.Equal("resend@example.com", recordingEmail.LastTo);
        Assert.Equal("resenduser", recordingEmail.LastUsername);
        Assert.False(string.IsNullOrWhiteSpace(recordingEmail.LastToken));
        Assert.False(string.Equals(recordingEmail.LastToken, "old-token", StringComparison.Ordinal));

        var refreshed = await context.Users.FirstAsync(u => u.Email == "resend@example.com");
        Assert.False(string.IsNullOrWhiteSpace(refreshed.VerificationToken));
        Assert.True(refreshed.VerificationTokenExpiry > DateTime.UtcNow);
    }

    [Fact]
    // Resend verification email should reject already verified accounts.
    public async Task ResendVerificationEmailAsync_ReturnsInvalidOperation_WhenAlreadyVerified()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var user = new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "verified@example.com",
            Username = "verifieduser",
            PasswordHash = Application.PasswordHasher.Hash("Password1!"),
            IsEmailVerified = true,
            VerificationToken = null,
            VerificationTokenExpiry = null
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.ResendVerificationEmailAsync("verified@example.com");

        Assert.False(result.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.InvalidOperation, result.Error!.Code);
    }

    [Fact]
    // Register a second user using an already existing email address.
    // The service should reject the request with DuplicateEmail.
    public async Task Register_ReturnsDuplicateEmail_WhenEmailExists()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        context.Users.Add(new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "existing",
            PasswordHash = Application.PasswordHasher.Hash("Password1!"),
            IsEmailVerified = true
        });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Register("test@example.com", "otheruser", "Password1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.DuplicateEmail, result.Error!.Code);
    }

    [Fact]
    // Register a second user using an already existing username.
    // The service should reject the request with DuplicateUsername.
    public async Task Register_ReturnsDuplicateUsername_WhenUsernameExists()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        context.Users.Add(new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "other@example.com",
            Username = "testuser",
            PasswordHash = Application.PasswordHasher.Hash("Password1!"),
            IsEmailVerified = true
        });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Register("test@example.com", "testuser", "Password1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.DuplicateUsername, result.Error!.Code);
    }

    [Theory]
    // Register failures when required registration fields are missing.
    [InlineData(null, "testuser", "Password1!")]
    [InlineData("test@example.com", null, "Password1!")]
    [InlineData("test@example.com", "testuser", null)]
    public async Task Register_ReturnsInvalidInput_WhenMissingRequiredFields(string? email, string? username, string? password)
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Register(email ?? string.Empty, username ?? string.Empty, password ?? string.Empty, "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.InvalidInput, result.Error!.Code);
    }

    [Fact]
    // Register failure when email format is invalid.
    public async Task Register_ReturnsInvalidInput_OnInvalidEmailFormat()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Register("invalid-email", "testuser", "Password1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.InvalidInput, result.Error!.Code);
    }

    [Fact]
    // Register should trim and normalize email and username.
    public async Task Register_TrimsAndNormalizesEmailAndUsername()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Register(" Test@Example.COM ", " TestUser ", "Password1!", "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal("test@example.com", result.Value.Email);
        Assert.Equal("TestUser", result.Value.Username);
    }

    [Fact]
    // Register should detect case-insensitive duplicate emails even with different casing.
    public async Task Register_DetectsCaseInsensitiveDuplicateEmail()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        context.Users.Add(new Domain.AppUser { Id = Guid.NewGuid(), Email = "test@example.com", Username = "u1", PasswordHash = Application.PasswordHasher.Hash("Password1!"), IsEmailVerified = true });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Register("TEST@EXAMPLE.com", "otheruser", "Password1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.DuplicateEmail, result.Error!.Code);
    }

    [Fact]
    // Register should detect case-insensitive duplicate usernames.
    public async Task Register_DetectsCaseInsensitiveDuplicateUsername()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        context.Users.Add(new Domain.AppUser { Id = Guid.NewGuid(), Email = "other@example.com", Username = "TestUser", PasswordHash = Application.PasswordHasher.Hash("Password1!"), IsEmailVerified = true });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Register("test@example.com", "testuser", "Password1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.DuplicateUsername, result.Error!.Code);
    }

    [Theory]
    // Username boundary checks: min 3 and max 30 allowed, outside not allowed.
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxx", true)] // 29 chars
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", true)] // 30 chars
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", false)] // 31 chars
    public async Task Register_UsernameLengthBoundaries(string username, bool shouldSucceed)
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Register("test@example.com", username, "Password1!", "127.0.0.1");
        Assert.Equal(shouldSucceed, result.IsSuccess);
    }

    [Theory]
    // Password policy edge-cases
    [InlineData("password1!", false)] // no upper
    [InlineData("PASSWORD1!", false)] // no lower
    [InlineData("Password!!", false)] // no digit
    [InlineData("Password1", false)] // no special
    [InlineData("Aa1!aaaa", true)] // exactly 8 with required classes
    public async Task Register_PasswordPolicyEdgeCases(string pwd, bool shouldSucceed)
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Register("test@example.com", "testuser", pwd, "127.0.0.1");
        Assert.Equal(shouldSucceed, result.IsSuccess);
    }

    [Theory]
    // Several invalid email patterns
    [InlineData("missing-at-sign.com")]
    [InlineData("missing-domain@")]
    [InlineData("email@domain..com")]
    [InlineData("space in@domain.com")]
    [InlineData("user@@domain.com")]
    public async Task Register_InvalidEmailPatterns(string badEmail)
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Register(badEmail, "testuser", "Password1!", "127.0.0.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.InvalidInput, result.Error!.Code);
    }

    [Fact]
    // Simulate DB unique constraint failure during SaveChanges -> expect DuplicateEmail result handling.
    public async Task Register_HandlesDbUpdateExceptionAsDuplicate()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        // use failing context to throw on SaveChanges
        using var context = new FailingAppDbContext(options);

        var service = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
        var result = await service.Register("test@example.com", "testuser", "Password1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.DuplicateEmail, result.Error!.Code);
    }

    [Fact]
    // Simulate a non-duplicate DB failure during SaveChanges -> expect InternalError result handling.
    public async Task Register_HandlesDbUpdateExceptionAsInternalError()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new FailingDbErrorAppDbContext(options);

        var service = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
        var result = await service.Register("test@example.com", "testuser", "Password1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.InternalError, result.Error!.Code);
    }

    [Fact]
    // Registration should still succeed when the email service throws; this proves the current server path can create a user without delivering email.
    public async Task Register_Succeeds_WhenEmailServiceThrows()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context, new ThrowingEmailService());

        var result = await service.Register("email-failure@example.com", "testuser", "Password1!", "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal("email-failure@example.com", result.Value.Email);
        Assert.Equal("testuser", result.Value.Username);
        var persisted = await context.Users.FirstOrDefaultAsync(u => u.Email == "email-failure@example.com");
        Assert.NotNull(persisted);
        Assert.False(persisted!.IsEmailVerified);
    }

    [Fact]
    // Rate limiting should still work when the request IP is null, using the unknown key.
    public async Task Register_ReturnsRateLimited_WhenTooManyAttemptsWithNullIp()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var rateLimitService = new InMemoryRateLimitService();
        var service = new AuthService(context, new InMemoryAuditService(), rateLimitService, new ConsoleEmailService());

        for (var i = 0; i < 3; i++)
        {
            var result = await service.Register($"nullip{i}@example.com", $"user{i}", "Password1!", null);
            Assert.True(result.IsSuccess);
        }

        var final = await service.Register("other@example.com", "user3", "Password1!", null);
        Assert.False(final.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.RateLimited, final.Error!.Code);
    }


    [Fact]
    // Register failure when password does not meet password complexity rules.
    public async Task Register_ReturnsInvalidInput_OnWeakPassword()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Register("test@example.com", "testuser", "weakpass", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.InvalidInput, result.Error!.Code);
    }

    [Fact]
    // Registration must be rate limited after three successful attempts from the same source.
    public async Task Register_ReturnsRateLimited_WhenTooManyAttempts()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var rateLimitService = new InMemoryRateLimitService();
        var service = new AuthService(context, new InMemoryAuditService(), rateLimitService, new ConsoleEmailService());

        for (var i = 0; i < 3; i++)
        {
            var result = await service.Register($"bad{i}@example.com", $"testuser{i}", "Password1!", "127.0.0.1");
            Assert.True(result.IsSuccess);
        }

        var final = await service.Register("other@example.com", "testuser3", "Password1!", "127.0.0.1");
        Assert.False(final.IsSuccess);
        Assert.NotNull(final.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.RateLimited, final.Error!.Code);
    }

    [Fact]
    // Login with valid credentials should succeed and return a user payload.
    public async Task Login_Succeeds_WithValidCredentials()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var password = "Password1!";
        var passwordHash = Application.PasswordHasher.Hash(password);

        context.Users.Add(new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = passwordHash,
            IsEmailVerified = true
        });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Login("testuser", password, "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("test@example.com", result.Value.Email);
    }

    [Fact]
    // Login should fail when required credentials are missing.
    public async Task Login_ReturnsInvalidInput_WhenMissingRequiredFields()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Login(string.Empty, string.Empty, "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.InvalidInput, result.Error!.Code);
    }

    [Fact]
    // Login should enforce rate limiting after repeated failed attempts.
    public async Task Login_ReturnsRateLimited_WhenTooManyAttempts()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var passwordHash = Application.PasswordHasher.Hash("Password1!");
        context.Users.Add(new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = passwordHash,
            IsEmailVerified = true
        });
        await context.SaveChangesAsync();

        var rateLimitService = new InMemoryRateLimitService();
        var service = new AuthService(context, new InMemoryAuditService(), rateLimitService, new ConsoleEmailService());

        for (var i = 0; i < 5; i++)
        {
            var attempt = await service.Login("testuser", "WrongPassword1!", "127.0.0.1");
            Assert.False(attempt.IsSuccess);
            Assert.Equal(Application.Results.ServiceErrorCode.InvalidCredentials, attempt.Error!.Code);
        }

        var final = await service.Login("testuser", "WrongPassword1!", "127.0.0.1");
        Assert.False(final.IsSuccess);
        Assert.NotNull(final.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.RateLimited, final.Error!.Code);
    }

    [Fact]
    // Login should trim the username before lookup (username lookup is case-insensitive).
    public async Task Login_TrimsAndHandlesUsername()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var pwd = "Password1!";
        context.Users.Add(new Domain.AppUser { Id = Guid.NewGuid(), Email = "test@example.com", Username = "testuser", PasswordHash = Application.PasswordHasher.Hash(pwd), IsEmailVerified = true });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Login(" TestUser ", pwd, "127.0.0.1");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    // Login with a non-existent user should return InvalidCredentials (dummy hash used internally).
    public async Task Login_NonExistentUser_ReturnsInvalidCredentials()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Login("nonexistentuser", "Password1!", "127.0.0.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.InvalidCredentials, result.Error!.Code);
    }

    [Theory]
    // Login with invalid/non-existent usernames should return InvalidCredentials and not throw.
    [InlineData("nonexistentuser1")]
    [InlineData("bad@user")]
    [InlineData("user@@domain")]
    public async Task Login_InvalidUsername_ReturnsInvalidCredentials(string badUsername)
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var service = CreateAuthService(context);

        var result = await service.Login(badUsername, "Password1!", "127.0.0.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.InvalidCredentials, result.Error!.Code);
    }

    [Fact]
    // When lockout has expired, login should succeed and reset lockout counters.
    public async Task Login_AfterLockoutExpiry_AllowsLoginAndResetsLockout()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var pwd = "Password1!";
        var user = new Domain.AppUser { Id = Guid.NewGuid(), Email = "test@example.com", Username = "tu", PasswordHash = Application.PasswordHasher.Hash(pwd), IsEmailVerified = true, LockoutEnd = DateTime.UtcNow.AddMinutes(-5), FailedLoginAttempts = 4 };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Login("tu", pwd, "127.0.0.1");

        Assert.True(result.IsSuccess);
        var refreshed = await context.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.Equal(0, refreshed.FailedLoginAttempts);
        Assert.Null(refreshed.LockoutEnd);
    }

    [Fact]
    // When the maximum number of wrong passwords is reached, the account should be locked.
    public async Task Login_LocksAccountAfterMaxFailedAttempts()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var passwordHash = Application.PasswordHasher.Hash("Password1!");

        context.Users.Add(new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = passwordHash,
            IsEmailVerified = true
        });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        for (var i = 0; i < 5; i++)
        {
            var attempt = await service.Login("testuser", "WrongPassword1!", "127.0.0.1");
            Assert.False(attempt.IsSuccess);
            Assert.Equal(Application.Results.ServiceErrorCode.InvalidCredentials, attempt.Error!.Code);
        }

        // The account should now be locked; using a fresh rate limiter avoids the rate-limit path.
        var retryService = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
        var lockedAttempt = await retryService.Login("testuser", "Password1!", "127.0.0.1");
        Assert.False(lockedAttempt.IsSuccess);
        Assert.Equal(Application.Results.ServiceErrorCode.AccountLocked, lockedAttempt.Error!.Code);

        var locked = await context.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.True(locked.LockoutEnd.HasValue);
        Assert.True(locked.LockoutEnd > DateTime.UtcNow);
    }

    [Fact]
    // After a successful login, the IP/email rate-limit should be reset.
    public async Task Login_ResetRateLimitAfterSuccess()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var pwd = "Password1!";
        context.Users.Add(new Domain.AppUser { Id = Guid.NewGuid(), Email = "test@example.com", Username = "tu", PasswordHash = Application.PasswordHasher.Hash(pwd), IsEmailVerified = true });
        await context.SaveChangesAsync();

        var rateLimitService = new InMemoryRateLimitService();
        var service = new AuthService(context, new InMemoryAuditService(), rateLimitService, new ConsoleEmailService());

        // one failed attempt
        var fail = await service.Login("tu", "WrongPass1!", "127.0.0.1");
        Assert.False(fail.IsSuccess);

        // successful login resets rate limit
        var ok = await service.Login("tu", pwd, "127.0.0.1");
        Assert.True(ok.IsSuccess);

        var allowed = await rateLimitService.IsAllowedAsync("login:tu", 5, TimeSpan.FromMinutes(15));
        Assert.True(allowed);
    }

    [Fact]
    // Login with wrong password should fail with InvalidCredentials.
    public async Task Login_ReturnsInvalidCredentials_OnWrongPassword()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var passwordHash = Application.PasswordHasher.Hash("Password1!");

        context.Users.Add(new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = passwordHash,
            IsEmailVerified = true
        });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Login("testuser", "WrongPassword1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.InvalidCredentials, result.Error!.Code);
    }

    [Fact]
    // Login should fail when the account is currently locked out.
    public async Task Login_ReturnsAccountLocked_WhenLockoutIsActive()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var passwordHash = Application.PasswordHasher.Hash("Password1!");

        context.Users.Add(new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = passwordHash,
            IsEmailVerified = true,
            LockoutEnd = DateTime.UtcNow.AddMinutes(10)
        });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Login("testuser", "Password1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.AccountLocked, result.Error!.Code);
    }

    [Fact]
    // Login should fail when the user's email is not verified yet.
    public async Task Login_ReturnsInvalidCredentials_WhenEmailNotVerified()
    {
        using var context = CreateInMemoryContext(Guid.NewGuid().ToString());
        var passwordHash = Application.PasswordHasher.Hash("Password1!");

        context.Users.Add(new Domain.AppUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = passwordHash,
            IsEmailVerified = false
        });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context);
        var result = await service.Login("testuser", "Password1!", "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Application.Results.ServiceErrorCode.EmailNotVerified, result.Error!.Code);
    }
}
