using System;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Application.Services;
using Serilog;
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
    private const int PasswordResetTokenExpiryHours = 1;

    private readonly string _baseUrl;
    private const int RefreshTokenDaysDefault = 30; // default refresh token validity period in days

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

    // Create and persist a refresh token for a user, returning the plaintext token
    public async Task<string> CreateRefreshTokenForUserAsync(Guid userId, string? createdByIp = null, int? days = null)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new ArgumentException("User not found", nameof(userId));

        // generate secure random token
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var tokenData = new byte[64];
        rng.GetBytes(tokenData);
        var token = Convert.ToBase64String(tokenData).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var tokenHash = PasswordHasher.Hash(token);
        var expires = DateTime.UtcNow.AddDays(days ?? RefreshTokenDaysDefault);

        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            Created = DateTime.UtcNow,
            Expires = expires,
            CreatedByIp = createdByIp,
            UserId = user.Id
        };

        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();

        return token;
    }

    // Validate a refresh token and rotate it (create a new one), returning the associated user and the new refresh token plaintext
    public async Task<Result<(UserDto User, string NewRefreshToken)>> ValidateAndRotateRefreshTokenAsync(string refreshToken, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return Result<(UserDto, string)>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Refresh token is required."));

        // find candidate tokens that are active
        var now = DateTime.UtcNow;
        var candidates = await _db.RefreshTokens.Include(rt => rt.User).Where(rt => rt.Revoked == null && rt.Expires > now).ToListAsync();

        RefreshToken? found = null;
        foreach (var cand in candidates)
        {
            if (PasswordHasher.Verify(refreshToken, cand.TokenHash))
            {
                found = cand;
                break;
            }
        }

        if (found == null)
        {
            return Result<(UserDto, string)>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid refresh token."));
        }

        var user = found.User;
        if (user == null)
        {
            return Result<(UserDto, string)>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid refresh token."));
        }

        if (found.Expires <= now)
        {
            return Result<(UserDto, string)>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Refresh token expired."));
        }

        // rotate: revoke current and create new
        found.Revoked = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(user.CurrentSessionId))
        {
            return Result<(UserDto, string)>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid refresh token."));
        }

        var newToken = await CreateRefreshTokenForUserAsync(user.Id, ipAddress, RefreshTokenDaysDefault);
        found.ReplacedByToken = "(rotated)";

        await _db.SaveChangesAsync();

        var userDto = new UserDto { Id = user.Id, Email = user.Email, Username = user.Username };
        return Result<(UserDto, string)>.Success((userDto, newToken));
    }

    public async Task<string?> GetCurrentSessionIdAsync(Guid userId)
    {
        return await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CurrentSessionId)
            .FirstOrDefaultAsync();
    }

    public async Task ClearCurrentSessionAsync(Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            user.CurrentSessionId = null;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<Result> RevokeRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return Result.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Refresh token is required."));

        var now = DateTime.UtcNow;
        var candidates = await _db.RefreshTokens.Include(rt => rt.User).Where(rt => rt.Revoked == null && rt.Expires > now).ToListAsync();

        RefreshToken? found = null;
        foreach (var cand in candidates)
        {
            if (PasswordHasher.Verify(refreshToken, cand.TokenHash))
            {
                found = cand;
                break;
            }
        }

        if (found == null)
        {
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid refresh token."));
        }

        found.Revoked = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }


    public async Task<Result<UserDto>> Login(string identifier, string password, string? ipAddress = null)
    {
        // validation
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            await _auditService.LogAuthEventAsync("login_attempt", identifier ?? "unknown", false, "Validation failed: empty input", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Username or email and password are required."));
        }

        // normalize identifier
        identifier = identifier.Trim();
        var isEmail = identifier.Contains('@') && IsValidEmail(identifier);
        var normalizedIdentifier = isEmail ? identifier.ToLowerInvariant() : identifier;

        // rate limiting: max 5 failed login attempts per 15 minutes, per identifier
        var rateLimitKey = $"login:{normalizedIdentifier.ToLowerInvariant()}";
        if (!await _rateLimitService.IsAllowedAsync(rateLimitKey, MaxFailedLoginAttempts, TimeSpan.FromMinutes(LoginAttemptWindowMinutes)))
        {
            await _auditService.LogAuthEventAsync("login_attempt", identifier, false, "Rate limit exceeded", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.RateLimited, "Too many login attempts. Try again later."));
        }

        // fetch user by email or username (case-insensitive); if not found, do a dummy password verify to mitigate timing attacks,
        // then return generic error without revealing which part was wrong.
        AppUser? user;
        try
        {
            if (isEmail)
            {
                user = await _db.Users.FirstOrDefaultAsync(x => x.Email == normalizedIdentifier);
            }
            else
            {
                user = await _db.Users.FirstOrDefaultAsync(x => x.Username.ToLower() == normalizedIdentifier.ToLower());
            }
        }
        catch (Exception ex)
        {
            await _auditService.LogAuthEventAsync("login_attempt", identifier, false, $"DB error: {ex.Message}", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InternalError, "Unable to access user store."));
        }

        // user not found.
        if (user == null)
        {
            PasswordHasher.Verify(password, _dummyHash);
            await _rateLimitService.RecordAttemptAsync(rateLimitKey);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid credentials."));
        }

        // prevent login for soft-deleted accounts
        if (user.IsDeleted)
        {
            await _auditService.LogAuthEventAsync("login_attempt", identifier, false, "Account deleted", ipAddress);
            await _rateLimitService.RecordAttemptAsync(rateLimitKey);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid credentials."));
        }

        // check if account is locked
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            await _auditService.LogAuthEventAsync("login_attempt", identifier, false, $"Account locked until {user.LockoutEnd:O}", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.AccountLocked, "Account is temporarily locked."));
        }

        // check email verification
        if (!user.IsEmailVerified)
        {
            await _auditService.LogAuthEventAsync("login_attempt", identifier, false, "Email not verified", ipAddress);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.EmailNotVerified, "Email address has not been verified."));
        }

        // verify password
        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);
                await _auditService.LogAuthEventAsync("login_attempt", identifier, false, $"Account locked after {user.FailedLoginAttempts} failed attempts", ipAddress);
            }
            else
            {
                await _auditService.LogAuthEventAsync("login_attempt", identifier, false, $"Wrong password (attempt {user.FailedLoginAttempts}/{MaxFailedLoginAttempts})", ipAddress);
            }

            await _db.SaveChangesAsync();
            await _rateLimitService.RecordAttemptAsync(rateLimitKey);
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid credentials."));
        }

        // successful login: reset failed attempts and lockout, revoke any previous refresh tokens, enforce a single active session.
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.CurrentSessionId = Guid.NewGuid().ToString();

        var now = DateTime.UtcNow;
        var activeTokens = await _db.RefreshTokens.Where(rt => rt.UserId == user.Id && rt.Revoked == null).ToListAsync();
        foreach (var rt in activeTokens)
        {
            rt.Revoked = now;
        }

        await _db.SaveChangesAsync();
        await _rateLimitService.ResetAsync(rateLimitKey);
        await _auditService.LogAuthEventAsync("login_success", identifier, true, null, ipAddress);

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
        if (!IsValidUsername(username))
            return Result<UserDto>.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Username must be between 3 and 30 characters and must not contain whitespace or '@'."));

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
            IsEmailVerified = false, // set to true to skip the email verification (if it doesnt work)
            VerificationToken = HashVerificationToken(verificationToken),
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
            Log.Debug("Prepared verification email for {Email} (link omitted from logs).", email);
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

    public async Task<Result> RequestPasswordResetAsync(string email, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return Result.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Email is required."));

        var normalized = email.Trim().ToLowerInvariant();

        // rate limit password reset attempts per email to avoid abuse
        var rlKey = $"pwreset:{normalized}";
        if (!await _rateLimitService.IsAllowedAsync(rlKey, 5, TimeSpan.FromHours(1)))
        {
            await _auditService.LogAuthEventAsync("password_reset_request", normalized, false, "Rate limit exceeded", null);
            // Still return success to avoid revealing that rate limiting occurred
            return Result.Success();
        }

        AppUser? user = null;
        try
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        }
        catch (Exception ex)
        {
            await _auditService.LogAuthEventAsync("password_reset_request", normalized, false, $"DB error: {ex.Message}", null);
            return Result.Failure(new ServiceError(ServiceErrorCode.InternalError, "Unable to access user store."));
        }

        // always record attempt
        await _rateLimitService.RecordAttemptAsync(rlKey);

        // Do not reveal whether the account exists to the caller. If user exists, create a reset token and email it.
        if (user != null)
        {
            // generate secure token
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var data = new byte[48];
            rng.GetBytes(data);
            var token = Convert.ToBase64String(data).Replace("+", "-").Replace("/", "_").TrimEnd('=');

            var tokenHash = PasswordHasher.Hash(token);
            user.PasswordResetToken = tokenHash;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(PasswordResetTokenExpiryHours);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch
            {
                // If we can't persist, do not reveal error to caller
                await _auditService.LogAuthEventAsync("password_reset_request", normalized, false, "DB save failed", null);
                return Result.Success();
            }

            var effectiveBase = string.IsNullOrWhiteSpace(baseUrl) ? _baseUrl : baseUrl.TrimEnd('/');
            var resetLink = $"{effectiveBase}/api/auth/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(normalized)}";

            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.Username, token, resetLink);
            }
            catch (Exception ex)
            {
                await _auditService.LogAuthEventAsync("password_reset_request", normalized, false, $"Email send failed: {ex.Message}", null);
                // don't surface email errors to caller
            }

            await _auditService.LogAuthEventAsync("password_reset_request", normalized, true, "Password reset token issued", null);
        }

        // return generic success regardless of whether user exists
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(string email, string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Email, token and new password are required."));

        if (!IsPasswordValid(newPassword))
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "Password does not meet complexity requirements."));

        var normalized = email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        if (user == null) return Result.Failure(new ServiceError(ServiceErrorCode.InvalidOperation, "Invalid token or email."));

        if (string.IsNullOrWhiteSpace(user.PasswordResetToken) || !user.PasswordResetTokenExpiry.HasValue || user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidOperation, "Reset token is missing or expired."));
        }

        if (!PasswordHasher.Verify(token, user.PasswordResetToken))
        {
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidOperation, "Invalid reset token."));
        }

        // apply password change
        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.CurrentSessionId = null;

        // Revoke all active refresh tokens for this user
        var now = DateTime.UtcNow;
        var tokens = await _db.RefreshTokens.Where(rt => rt.UserId == user.Id && rt.Revoked == null).ToListAsync();
        foreach (var rt in tokens)
        {
            rt.Revoked = now;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAuthEventAsync("password_reset", normalized, true, "Password reset completed", null);

        return Result.Success();
    }

    // Soft-delete a user account: revoke tokens, anonymize PII, and mark as deleted.
    public async Task<Result> DeleteAccountAsync(Guid userId, string? ipAddress = null)
    {
        var user = await _db.Users.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Result.Failure(new ServiceError(ServiceErrorCode.InvalidInput, "User not found."));

        if (user.IsDeleted)
        {
            // idempotent
            await _auditService.LogAuthEventAsync("delete_account", user.Email, true, "Already deleted", ipAddress);
            return Result.Success();
        }

        // capture original identifying info for the audit
        var originalEmail = user.Email;
        var originalUsername = user.Username;

        // Revoke active refresh tokens
        var now = DateTime.UtcNow;
        if (user.RefreshTokens != null)
        {
            foreach (var rt in user.RefreshTokens.Where(t => t.Revoked == null))
            {
                rt.Revoked = now;
            }
        }

        // Anonymize PII and make the account unusable
        user.Email = $"deleted+{user.Id}@deleted.local";
        user.Username = $"deleted_{user.Id}";
        user.PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString());
        user.IsEmailVerified = false;
        user.VerificationToken = null;
        user.VerificationTokenExpiry = null;
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.CurrentSessionId = null;
        user.IsDeleted = true;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await _auditService.LogAuthEventAsync("delete_account", originalEmail ?? originalUsername, false, $"DB error: {ex.Message}", ipAddress);
            return Result.Failure(new ServiceError(ServiceErrorCode.InternalError, "Failed to delete user."));
        }

        await _auditService.LogAuthEventAsync("delete_account", originalEmail ?? originalUsername, true, "User soft-deleted", ipAddress);
        return Result.Success();
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

    private static bool IsValidUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        if (username.Length < 3 || username.Length > 30) return false;
        if (username.Any(char.IsWhiteSpace)) return false;
        if (username.Contains('@')) return false;
        return true;
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

        // validate token and expiry
        if (string.IsNullOrWhiteSpace(user.VerificationToken) || !user.VerificationTokenExpiry.HasValue || user.VerificationTokenExpiry < DateTime.UtcNow)
        {
            await _auditService.LogAuthEventAsync("email_verify_attempt", email, false, "Invalid or expired token", null);
            return Result.Failure(new ServiceError(ServiceErrorCode.InvalidCredentials, "Invalid or expired verification token."));
        }

        if (!VerifyVerificationToken(token, user.VerificationToken))
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
        var newToken = GenerateVerificationToken();
        user.VerificationToken = HashVerificationToken(newToken);
        user.VerificationTokenExpiry = DateTime.UtcNow.AddHours(EmailVerificationTokenExpiryHours);
        await _db.SaveChangesAsync();

        // determine effective base URL
        var effectiveBase = string.IsNullOrWhiteSpace(baseUrl) ? _baseUrl : baseUrl.TrimEnd('/');
        var verificationLink =
            $"{effectiveBase}/api/auth/verify-email" +
            $"?token={Uri.EscapeDataString(newToken)}" +
            $"&email={Uri.EscapeDataString(email)}";

            Log.Debug("Prepared resend verification email for {Email} (link omitted from logs).", email);
        await _emailService.SendVerificationEmailAsync(email, user.Username, newToken, verificationLink);
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

    private static string HashVerificationToken(string token)
    {
        if (string.IsNullOrEmpty(token)) throw new ArgumentException("Token must not be empty.", nameof(token));
        return PasswordHasher.Hash(token);
    }

    private static bool VerifyVerificationToken(string providedToken, string storedTokenHash)
    {
        if (string.IsNullOrWhiteSpace(providedToken) || string.IsNullOrWhiteSpace(storedTokenHash))
            return false;

        return PasswordHasher.Verify(providedToken, storedTokenHash);
    }
}