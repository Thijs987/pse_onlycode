using System;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Application.Services;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;
    private readonly IRateLimitService _rateLimitService;
    private static readonly string _dummyHash = PasswordHasher.Hash("__dummy_password_for_timing__");

    // Configuration
    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutDurationMinutes = 15;
    private const int LoginAttemptWindowMinutes = 15;
    private const int RegisterAttemptWindowMinutes = 30;

    public AuthService(AppDbContext db, IAuditService auditService, IRateLimitService rateLimitService)
    {
        _db = db;
        _auditService = auditService;
        _rateLimitService = rateLimitService;
    }


    public async Task<UserDto?> Login(string email, string password, string? ipAddress = null)
    {
        // basic validation
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await _auditService.LogAuthEventAsync("login_attempt", email ?? "unknown", false, "Validation failed: empty input", ipAddress);
            return null;
        }

        // normalize email
        email = email.Trim().ToLowerInvariant();

        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            /* Treat invalid email format as authentication failure without throwing
               to avoid revealing information to callers.
               Run a "dummy verify" to make timing similar to a real verification. */
            await _auditService.LogAuthEventAsync("login_attempt", email, false, "Validation failed: invalid email format", ipAddress);
            PasswordHasher.Verify(password, _dummyHash);
            return null;
        }

        // rate limiting: max 5 failed login attempts per 15 minutes per email
        var rateLimitKey = $"login:{email}";
        var allowed = await _rateLimitService.IsAllowedAsync(rateLimitKey, MaxFailedLoginAttempts, TimeSpan.FromMinutes(LoginAttemptWindowMinutes));
        if (!allowed)
        {
            await _auditService.LogAuthEventAsync("login_attempt", email, false, "Rate limit exceeded", ipAddress);
            return null;
        }

        // find user by email
        AppUser? user;
        try
        {
            user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email);
        }
        catch (Exception ex)
        {
            await _auditService.LogAuthEventAsync("login_attempt", email, false, $"DB error: {ex.Message}", ipAddress);
            throw new InvalidOperationException("Failed to access user store.", ex);
        }

        // if user not found...
        if (user == null)
        {
            /* Mitigate user-existence timing attacks by performing a password
               verification against a constant dummy hash so callers can't observe
               large time differences between "user not found" and "bad password" */
            PasswordHasher.Verify(password, _dummyHash);
            await _rateLimitService.RecordAttemptAsync(rateLimitKey);
            return null;
        }

        // check if account is locked out
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            await _auditService.LogAuthEventAsync("login_attempt", email, false, $"Account locked until {user.LockoutEnd:O}", ipAddress);
            return null;
        }

        // check email verification
        if (!user.IsEmailVerified)
        {
            await _auditService.LogAuthEventAsync("login_attempt", email, false, "Email not verified", ipAddress);
            return null;
        }

        // verify password
        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            // increment failed attempts and lock if needed
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
            return null;
        }

        // successful login: reset failed attempts and rate limit
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync();
        await _rateLimitService.ResetAsync(rateLimitKey);
        await _auditService.LogAuthEventAsync("login_success", email, true, null, ipAddress);

        return new UserDto { Id = user.Id, Email = user.Email, Username = user.Username };
    }

    public async Task<UserDto> Register(string email, string username, string password, string? ipAddress = null)
    {
        // rate limiting: max 3 register attempts per 30 minutes, per IP
        var rateLimitKey = $"register:{ipAddress ?? "unknown"}";
        var allowed = await _rateLimitService.IsAllowedAsync(rateLimitKey, 3, TimeSpan.FromMinutes(RegisterAttemptWindowMinutes));
        if (!allowed)
        {
            await _auditService.LogAuthEventAsync("register_attempt", email ?? "unknown", false, "Rate limit exceeded", ipAddress);
            throw new InvalidOperationException("Too many registration attempts. Please try again later.");
        }

        // basic validation
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required.", nameof(password));

        // normalize email and username
        email = email.Trim().ToLowerInvariant();
        username = username.Trim();

        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            await _auditService.LogAuthEventAsync("register_attempt", email, false, "Invalid email format", ipAddress);
            throw new ArgumentException("Invalid email format.", nameof(email));
        }

        // username check + valid password check
        if (username.Length < 3 || username.Length > 30)
            throw new ArgumentException("Username must be between 3 and 30 characters.", nameof(username));

        if (!IsPasswordValid(password))
            throw new ArgumentException("Password does not meet complexity requirements. Use at least 8 characters including upper, lower, digit and special character.", nameof(password));

        // checks if email or username is already taken
        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new InvalidOperationException("Email is already in use.");

        if (await _db.Users.AnyAsync(u => u.Username == username))
            throw new InvalidOperationException("Username is already in use.");

        // create user
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = PasswordHasher.Hash(password)
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Try to detect common unique constraint violations and return a helpful message.
            var inner = ex.InnerException?.Message ?? string.Empty;
            if (!string.IsNullOrEmpty(inner) && (inner.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("constraint", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Email or username already exists.", ex);
            }

            throw new InvalidOperationException("Failed to create user.", ex);
        }

        return new UserDto { Id = user.Id, Email = user.Email, Username = user.Username };
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
}