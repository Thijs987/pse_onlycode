using System;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Application.Results;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application;

public class AuthService
{
    // dependencies
    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;
    private readonly IRateLimitService _rateLimitService;
    private readonly IEmailService _emailService;
    //private readonly IConfiguration _config;
    private static readonly string _dummyHash = PasswordHasher.Hash("__dummy_password_for_timing__");

    // Configuration
    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutDurationMinutes = 15;
    private const int LoginAttemptWindowMinutes = 15;
    private const int RegisterAttemptWindowMinutes = 30;
    private const int EmailVerificationTokenExpiryHours = 24;

    private readonly string _baseUrl;

    public AuthService(AppDbContext db, IAuditService auditService, IRateLimitService rateLimitService, IEmailService emailService, IConfiguration? config = null)
    {
        _db = db;
        _auditService = auditService;
        _rateLimitService = rateLimitService;
        _emailService = emailService;

        // Read base URL from configuration (AppSettings:BaseUrl) or environment variable, fallback to previous hardcoded value
        var baseUrl = config? ["AppSettings:BaseUrl"] ?? Environment.GetEnvironmentVariable("APP_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://83.96.203.15:5000";
        }

        // ensure no trailing slash
        _baseUrl = baseUrl.TrimEnd('/');
    }


    public async Task<Result<UserDto>> Login(string email, string password, string? ipAddress = null)
    {
        // validation
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await _auditService.LogAuthEventAsync("login_attempt", email ?? "unknown", false, "Validation failed: empty input", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Email and password are required."));
        }

        // normalize email
        email = email.Trim().ToLowerInvariant();
        if (!IsValidEmail(email))
        {
            await _auditService.LogAuthEventAsync("login_attempt", email, false, "Validation failed: invalid email format", ipAddress);
            PasswordHasher.Verify(password, _dummyHash);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid credentials."));
        }

        // rate limiting: max 5 failed login attempts per 15 minutes, per email
        var rateLimitKey = $"login:{email}";
        if (!await _rateLimitService.IsAllowedAsync(rateLimitKey, MaxFailedLoginAttempts, TimeSpan.FromMinutes(LoginAttemptWindowMinutes)))
        {
            await _auditService.LogAuthEventAsync("login_attempt", email, false, "Rate limit exceeded", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.RateLimited, "Too many login attempts. Try again later."));
        }

        // fetch user by email; if not found, do a dummy password verify to mitigate timing attacks,
        // then return generic error without revealing which part was wrong (this extra security
        // measure is done everywhere below here).
        AppUser? user;
        try
        {
            user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);
        }
        catch (Exception ex)
        {
            await _auditService.LogAuthEventAsync("login_attempt", email, false, $"DB error: {ex.Message}", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InternalError, "Unable to access user store."));
        }

        // user not found.
        if (user == null)
        {
            PasswordHasher.Verify(password, _dummyHash);
            await _rateLimitService.RecordAttemptAsync(rateLimitKey);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid credentials."));
        }

        // check if account is locked
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            await _auditService.LogAuthEventAsync("login_attempt", email, false, $"Account locked until {user.LockoutEnd:O}", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.AccountLocked, "Account is temporarily locked."));
        }

        // check email verification
        if (!user.IsEmailVerified)
        {
            await _auditService.LogAuthEventAsync("login_attempt", email, false, "Email not verified", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.EmailNotVerified, "Email address has not been verified."));
        }

        // verify password
        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);
                await _auditService.LogAuthEventAsync("login_attempt", email, false, $"Account locked after {user.FailedLoginAttempts} failed attempts", ipAddress);
            }
            else
            {
                await _auditService.LogAuthEventAsync("login_attempt", email, false, $"Wrong password (attempt {user.FailedLoginAttempts}/{MaxFailedLoginAttempts})", ipAddress);
            }

            await _db.SaveChangesAsync();
            await _rateLimitService.RecordAttemptAsync(rateLimitKey);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid credentials."));
        }

        // successful login: reset failed attempts and lockout, log success, return user info
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync();
        await _rateLimitService.ResetAsync(rateLimitKey);
        await _auditService.LogAuthEventAsync("login_success", email, true, null, ipAddress);

        return Result<UserDto>.Success(new UserDto { Id = user.Id, Email = user.Email, Username = user.Username });
    }

    public async Task<Result<UserDto>> Register(string email, string username, string password, string? ipAddress = null, string? baseUrl = null)
    {
        // rate limiting: max 3 register attempts per 30 minutes, per IP
        var rateLimitKey = $"register:{ipAddress ?? "unknown"}";
        var allowed = await _rateLimitService.IsAllowedAsync(rateLimitKey, 3, TimeSpan.FromMinutes(RegisterAttemptWindowMinutes));
        if (!allowed)
        {
            await _auditService.LogAuthEventAsync("register_attempt", email ?? "unknown", false, "Rate limit exceeded", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.RateLimited, "Too many registration attempts. Please try again later."));
        }

        // basic validation
        if (string.IsNullOrWhiteSpace(email)) return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Email is required."));
        if (string.IsNullOrWhiteSpace(username)) return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Username is required."));
        if (string.IsNullOrWhiteSpace(password)) return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Password is required."));

        // normalize email and username
        email = email.Trim().ToLowerInvariant();
        username = username.Trim();

        if (!IsValidEmail(email))
        {
            await _auditService.LogAuthEventAsync("register_attempt", email, false, "Invalid email format", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Invalid email format."));
        }

        // username check + valid password check
        if (username.Length < 3 || username.Length > 30)
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Username must be between 3 and 30 characters."));

        if (!IsPasswordValid(password))
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Password does not meet complexity requirements. Use at least 8 characters including upper, lower, digit and special character."));

        // checks if email or username is already taken
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            await _auditService.LogAuthEventAsync("register_attempt", email, false, "Email already in use", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.DuplicateEmail, "Email is already in use."));
        }

        var normalizedUsername = username.ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername))
        {
            await _auditService.LogAuthEventAsync("register_attempt", email, false, "Username already in use", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.DuplicateUsername, "Username is already in use."));
        }

        // generate email verification token (valid for 24 hours)
        var verificationToken = GenerateVerificationToken();
        var verificationExpiry = DateTime.UtcNow.AddHours(EmailVerificationTokenExpiryHours);

        // create user (email not verified yet)
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            IsEmailVerified = false, // set to false if you want to require email verification before login
            VerificationToken = verificationToken,
            VerificationTokenExpiry = verificationExpiry
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? string.Empty;
            if (!string.IsNullOrEmpty(inner) && (inner.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("constraint", StringComparison.OrdinalIgnoreCase)))
            {
                await _auditService.LogAuthEventAsync("register_attempt", email, false, "Duplicate key (race condition)", ipAddress);
                return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.DuplicateEmail, "Email or username already exists."));
            }

            await _auditService.LogAuthEventAsync("register_attempt", email, false, $"DB error: {ex.Message}", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InternalError, "Failed to create user."));
        }

        // determine which base URL to use: prefer the provided baseUrl (from request), then configured _baseUrl
        var effectiveBase = string.IsNullOrWhiteSpace(baseUrl) ? _baseUrl : baseUrl.TrimEnd('/');
        var verificationLink =
            $"{effectiveBase}/api/auth/verify-email" +
            $"?token={Uri.EscapeDataString(verificationToken)}" +
            $"&email={Uri.EscapeDataString(email)}";
        try
        {
            Console.WriteLine($"[VERIFICATION] Link for {email}: {verificationLink}");
            await _emailService.SendVerificationEmailAsync(email, username, verificationToken, verificationLink);
        }
        catch (Exception ex)
        {
            // log but don't fail registration; user can request resend
            await _auditService.LogAuthEventAsync("register_attempt", email, true, $"Email send failed: {ex.Message}", ipAddress);
        }

        await _auditService.LogAuthEventAsync("register_attempt", email, true, "User created, email verification required", ipAddress);
        await _rateLimitService.RecordAttemptAsync(rateLimitKey);

        return Result<UserDto>.Success(new UserDto { Id = user.Id, Email = user.Email, Username = user.Username });
    }

    private static bool IsPasswordValid(string password)
    {
        /* Checks if password has the following requirements:
            - at least 8 characters long
            - contain upper, lower, digit
            - contain special character 
            Returns true if valid, false otherwise */
        if (password.Length < 8) return false;

        var hasUpper = Regex.IsMatch(password, "[A-Z]");
        var hasLower = Regex.IsMatch(password, "[a-z]");
        var hasDigit = Regex.IsMatch(password, "[0-9]");
        var hasSpecial = Regex.IsMatch(password, "[^a-zA-Z0-9]");

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        if (email.Any(char.IsWhiteSpace))
            return false;
        if (email.Count(c => c == '@') != 1)
            return false;

        var parts = email.Split('@');
        if (parts.Length != 2)
            return false;
        var local = parts[0];
        var domain = parts[1];
        if (string.IsNullOrWhiteSpace(local) || string.IsNullOrWhiteSpace(domain))
            return false;
        if (local.StartsWith('.') || local.EndsWith('.') || domain.StartsWith('.') || domain.EndsWith('.'))
            return false;
        if (local.Contains("..") || domain.Contains(".."))
            return false;
        if (!domain.Contains('.'))
            return false;

        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Result> VerifyEmailAsync(string email, string token)
    {
        // basic validation
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            await _auditService.LogAuthEventAsync("email_verify_attempt", email ?? "unknown", false, "Empty email or token", null);
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Invalid verification request."));
        }

        // normalize email + find user by email
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            await _auditService.LogAuthEventAsync("email_verify_attempt", email, false, "User not found", null);
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid verification link."));
        }

        // check if already verified
        if (user.IsEmailVerified)
        {
            await _auditService.LogAuthEventAsync("email_verify_attempt", email, false, "Email already verified", null);
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidOperation, "Email already verified."));
        }

        // check token and expiry
        if (user.VerificationToken != token || !user.VerificationTokenExpiry.HasValue ||
                                                user.VerificationTokenExpiry < DateTime.UtcNow)
        {
            await _auditService.LogAuthEventAsync("email_verify_attempt", email, false, "Invalid or expired token", null);
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid or expired verification token."));
        }

        // mark email as verified and clear token
        user.IsEmailVerified = true;
        user.VerificationToken = null;
        user.VerificationTokenExpiry = null;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await _auditService.LogAuthEventAsync("email_verify_attempt", email, false, $"DB error: {ex.Message}", null);
            throw;
        }

        await _auditService.LogAuthEventAsync("email_verify_attempt", email, true, null, null);
        return Result.Success();
    }

    public async Task<Result> ResendVerificationEmailAsync(string email, string? baseUrl = null)
    {
        // basic validation
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Email is required."));

        // normalize email + find user by email
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || user.IsEmailVerified)
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidOperation, "Invalid request."));

        // generate new token and expiry
        user.VerificationToken = GenerateVerificationToken();
        user.VerificationTokenExpiry = DateTime.UtcNow.AddHours(EmailVerificationTokenExpiryHours);
        await _db.SaveChangesAsync();

        // determine effective base URL
        var effectiveBase = string.IsNullOrWhiteSpace(baseUrl) ? _baseUrl : baseUrl.TrimEnd('/');
        var verificationLink =
            $"{effectiveBase}/api/auth/verify-email" +
            $"?token={Uri.EscapeDataString(user.VerificationToken)}" +
            $"&email={Uri.EscapeDataString(email)}";

        Console.WriteLine($"[VERIFICATION] Resend link for {email}: {verificationLink}");
        await _emailService.SendVerificationEmailAsync(email, user.Username, user.VerificationToken, verificationLink);
        await _auditService.LogAuthEventAsync("resend_verification", email, true, null, null);

        return Result.Success();
    }

    private static string GenerateVerificationToken()
    {
        // generate a secure random token (32 bytes, base64url encoded)
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var tokenData = new byte[32];
        rng.GetBytes(tokenData);
        return Convert.ToBase64String(tokenData).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}