using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Application;
using Application.Results;
using Application.Services;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Comprehensive security integration tests for JWT, refresh tokens, CSRF, and auth flows.
/// Tests cover: JWT validation, refresh token lifecycle, email verification, account lockout,
/// password hashing, and rate limiting.
/// </summary>
public class SecurityFeaturesIntegrationTests
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

    #region JWT Token Tests

    [Fact]
    public void JwtToken_ContainsCorrectClaims()
    {
        var jwtKey = "test-jwt-key-must-be-at-least-32-characters-long";
        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var userId = Guid.NewGuid();
        var userEmail = "test@example.com";
        var username = "testuser";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, userEmail),
            new Claim("username", username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "PSE-Green",
            audience: "PSE-Green-Clients",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var parsedToken = new JwtSecurityTokenHandler().ReadToken(tokenString) as JwtSecurityToken;

        Assert.NotNull(parsedToken);
        Assert.Equal("PSE-Green", parsedToken.Issuer);
        Assert.NotNull(parsedToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString()));
        Assert.NotNull(parsedToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == userEmail));
        Assert.NotNull(parsedToken.Claims.FirstOrDefault(c => c.Type == "username" && c.Value == username));
    }

    [Fact]
    public void JwtToken_SignatureValidationWithWrongKeyFails()
    {
        var validKey = "test-jwt-key-must-be-at-least-32-characters-long";
        var invalidKey = "invalid-key-must-be-at-least-32chars";

        var validKeyBytes = Encoding.UTF8.GetBytes(validKey);
        var invalidKeyBytes = Encoding.UTF8.GetBytes(invalidKey);

        var validSigningKey = new SymmetricSecurityKey(validKeyBytes);
        var validCreds = new SigningCredentials(validSigningKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "PSE-Green",
            audience: "PSE-Green-Clients",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: validCreds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var invalidSigningKey = new SymmetricSecurityKey(invalidKeyBytes);

        // Signature validation should fail
        var handler = new JwtSecurityTokenHandler();
        var exception = Record.Exception(() =>
        {
            handler.ValidateToken(tokenString, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = invalidSigningKey,
                ValidateIssuer = false,
                ValidateAudience = false
            }, out _);
        });

        Assert.NotNull(exception);
        Assert.IsAssignableFrom<SecurityTokenException>(exception);
    }

    [Fact]
    public void JwtToken_IssuerAndAudienceValidationFails()
    {
        var jwtKey = "test-jwt-key-must-be-at-least-32-characters-long";
        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "WrongIssuer",
            audience: "WrongAudience",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var handler = new JwtSecurityTokenHandler();
        var exception = Record.Exception(() =>
        {
            handler.ValidateToken(tokenString, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = true,
                ValidIssuer = "PSE-Green",
                ValidateAudience = true,
                ValidAudience = "PSE-Green-Clients"
            }, out _);
        });

        Assert.NotNull(exception);
        Assert.IsAssignableFrom<SecurityTokenException>(exception);
    }

    [Fact]
    public void JwtToken_ExpiryValidationFails()
    {
        var jwtKey = "test-jwt-key-must-be-at-least-32-characters-long";
        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var expiredToken = new JwtSecurityToken(
            issuer: "PSE-Green",
            audience: "PSE-Green-Clients",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(expiredToken);

        var handler = new JwtSecurityTokenHandler();
        var exception = Record.Exception(() =>
        {
            handler.ValidateToken(tokenString, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
        });

        Assert.NotNull(exception);
        Assert.IsAssignableFrom<SecurityTokenException>(exception);
    }

    [Fact]
    public void JwtToken_WithClockSkewAllowsSlightTimeDeviation()
    {
        var jwtKey = "test-jwt-key-must-be-at-least-32-characters-long";
        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "PSE-Green",
            audience: "PSE-Green-Clients",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddSeconds(-10),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var result = new JwtSecurityTokenHandler().ValidateToken(tokenString, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = "PSE-Green",
            ValidateAudience = true,
            ValidAudience = "PSE-Green-Clients",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        }, out var validatedToken);

        Assert.NotNull(result);
        Assert.NotNull(validatedToken);
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshToken_IsStoredHashedInDatabase()
    {
        if (!HasIntegrationConnection()) return;

        Guid? createdUserId = null;
        await using var context = CreateContext();
        try
        {
            var userId = Guid.NewGuid();
            var user = new AppUser
            {
                Id = userId,
                Email = $"refreshtest{Guid.NewGuid():N}@test.com",
                Username = $"ruser{Guid.NewGuid():N}"[..30],
                PasswordHash = PasswordHasher.Hash("Password1!"),
                IsEmailVerified = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            createdUserId = userId;

            var authService = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
            var plainToken = await authService.CreateRefreshTokenForUserAsync(userId, "127.0.0.1", 30);

            Assert.NotNull(plainToken);
            var storedToken = context.RefreshTokens.FirstOrDefault(rt => rt.UserId == userId);
            Assert.NotNull(storedToken);
            Assert.NotEqual(plainToken, storedToken.TokenHash);
            Assert.Null(storedToken.Revoked);
        }
        finally
        {
            if (createdUserId.HasValue)
            {
                await CleanupIntegrationUserAsync(context, createdUserId.Value);
            }
        }
    }

    [Fact]
    public async Task RefreshToken_CanBeRotated()
    {
        if (!HasIntegrationConnection()) return;

        Guid? createdUserId = null;
        await using var context = CreateContext();
        try
        {
            var userId = Guid.NewGuid();
            var user = new AppUser
            {
                Id = userId,
                Email = $"rotatetest{Guid.NewGuid():N}@test.com",
                Username = $"rotuser{Guid.NewGuid():N}"[..30],
                PasswordHash = PasswordHasher.Hash("Password1!"),
                IsEmailVerified = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            createdUserId = userId;

            var authService = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
            var oldToken = await authService.CreateRefreshTokenForUserAsync(userId, "127.0.0.1");

            var rotateResult = await authService.ValidateAndRotateRefreshTokenAsync(oldToken, "127.0.0.1");
            Assert.True(rotateResult.IsSuccess);

            var (returnedUser, newToken) = rotateResult.Value;
            Assert.Equal(userId, returnedUser.Id);
            Assert.NotEqual(oldToken, newToken);

            var oldStored = context.RefreshTokens.FirstOrDefault(rt => rt.UserId == userId && rt.Revoked != null);
            var newStored = context.RefreshTokens.FirstOrDefault(rt => rt.UserId == userId && rt.Revoked == null);
            Assert.NotNull(oldStored);
            Assert.NotNull(newStored);
        }
        finally
        {
            if (createdUserId.HasValue)
            {
                await CleanupIntegrationUserAsync(context, createdUserId.Value);
            }
        }
    }

    [Fact]
    public async Task RefreshToken_CanBeRevoked()
    {
        if (!HasIntegrationConnection()) return;

        Guid? createdUserId = null;
        await using var context = CreateContext();
        try
        {
            var userId = Guid.NewGuid();
            var user = new AppUser
            {
                Id = userId,
                Email = $"revoketest{Guid.NewGuid():N}@test.com",
                Username = $"revokeuser{Guid.NewGuid():N}"[..30],
                PasswordHash = PasswordHasher.Hash("Password1!"),
                IsEmailVerified = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            createdUserId = userId;

            var authService = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
            var token = await authService.CreateRefreshTokenForUserAsync(userId, "127.0.0.1");

            var revokeResult = await authService.RevokeRefreshTokenAsync(token);
            Assert.True(revokeResult.IsSuccess);

            var revokedToken = context.RefreshTokens.FirstOrDefault(rt => rt.UserId == userId);
            Assert.NotNull(revokedToken);
            Assert.NotNull(revokedToken.Revoked);
        }
        finally
        {
            if (createdUserId.HasValue)
            {
                await CleanupIntegrationUserAsync(context, createdUserId.Value);
            }
        }
    }

    [Fact]
    public async Task RefreshToken_InvalidTokenFails()
    {
        if (!HasIntegrationConnection()) return;

        await using var context = CreateContext();
        var authService = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());

        var result = await authService.ValidateAndRotateRefreshTokenAsync("invalid-token", "127.0.0.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorCode.InvalidCredentials, result.Error?.Code);
    }

    #endregion

    #region Auth & Security Tests

    [Fact]
    public async Task LoginRequiresEmailVerification()
    {
        if (!HasIntegrationConnection()) return;

        Guid? createdUserId = null;
        await using var context = CreateContext();
        try
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = $"unverified{Guid.NewGuid():N}@test.com",
                Username = $"unverifieduser{Guid.NewGuid():N}"[..30],
                PasswordHash = PasswordHasher.Hash("Password1!"),
                IsEmailVerified = false,
                VerificationToken = "token",
                VerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            createdUserId = user.Id;

            var authService = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
            var result = await authService.Login(user.Username, "Password1!", "127.0.0.1");

            Assert.False(result.IsSuccess);
            Assert.Equal(ServiceErrorCode.EmailNotVerified, result.Error?.Code);
        }
        finally
        {
            if (createdUserId.HasValue)
            {
                await CleanupIntegrationUserAsync(context, createdUserId.Value);
            }
        }
    }

    [Fact]
    public async Task AccountLockoutPreventsLogin()
    {
        if (!HasIntegrationConnection()) return;

        Guid? createdUserId = null;
        await using var context = CreateContext();
        try
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = $"lockout{Guid.NewGuid():N}@test.com",
                Username = $"lockeduser{Guid.NewGuid():N}"[..30],
                PasswordHash = PasswordHasher.Hash("Password1!"),
                IsEmailVerified = true,
                FailedLoginAttempts = 5,
                LockoutEnd = DateTime.UtcNow.AddMinutes(15)
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            createdUserId = user.Id;

            var authService = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
            var result = await authService.Login(user.Username, "Password1!", "127.0.0.1");

            Assert.False(result.IsSuccess);
            Assert.Equal(ServiceErrorCode.AccountLocked, result.Error?.Code);
        }
        finally
        {
            if (createdUserId.HasValue)
            {
                await CleanupIntegrationUserAsync(context, createdUserId.Value);
            }
        }
    }

    [Fact]
    public async Task EmailVerificationWorks()
    {
        if (!HasIntegrationConnection()) return;

        Guid? createdUserId = null;
        await using var context = CreateContext();
        try
        {
            var email = $"verify{Guid.NewGuid():N}@test.com";
            var token = "test-token-123";
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = $"verifyuser{Guid.NewGuid():N}"[..30],
                PasswordHash = PasswordHasher.Hash("Password1!"),
                IsEmailVerified = false,
                VerificationToken = PasswordHasher.Hash(token),
                VerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            createdUserId = user.Id;

            var authService = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
            var result = await authService.VerifyEmailAsync(email, token);

            Assert.True(result.IsSuccess);
            var verified = context.Users.FirstOrDefault(u => u.Email == email);
            Assert.NotNull(verified);
            Assert.True(verified.IsEmailVerified);
            Assert.Null(verified.VerificationToken);
        }
        finally
        {
            if (createdUserId.HasValue)
            {
                await CleanupIntegrationUserAsync(context, createdUserId.Value);
            }
        }
    }

    [Fact]
    public async Task ExpiredVerificationTokenFails()
    {
        if (!HasIntegrationConnection()) return;

        Guid? createdUserId = null;
        await using var context = CreateContext();
        try
        {
            var email = $"expiredverify{Guid.NewGuid():N}@test.com";
            var token = "expired-token";
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                Username = $"expireduser{Guid.NewGuid():N}"[..30],
                PasswordHash = PasswordHasher.Hash("Password1!"),
                IsEmailVerified = false,
                VerificationToken = PasswordHasher.Hash(token),
                VerificationTokenExpiry = DateTime.UtcNow.AddHours(-1)
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            createdUserId = user.Id;

            var authService = new AuthService(context, new InMemoryAuditService(), new InMemoryRateLimitService(), new ConsoleEmailService());
            var result = await authService.VerifyEmailAsync(email, token);

            Assert.False(result.IsSuccess);
            Assert.Equal(ServiceErrorCode.InvalidCredentials, result.Error?.Code);
        }
        finally
        {
            if (createdUserId.HasValue)
            {
                await CleanupIntegrationUserAsync(context, createdUserId.Value);
            }
        }
    }

    #endregion

    #region Password Hashing Tests

    [Fact]
    public void PasswordHasher_HashesAndVerifiesCorrectly()
    {
        var password = "Test@Password123!";
        var hash = PasswordHasher.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.True(PasswordHasher.Verify(password, hash));
        Assert.False(PasswordHasher.Verify("WrongPassword", hash));
    }

    [Fact]
    public void PasswordHasher_GeneratesDifferentHashesForSamePassword()
    {
        var password = "Test@Password123!";
        var hash1 = PasswordHasher.Hash(password);
        var hash2 = PasswordHasher.Hash(password);

        Assert.NotEqual(hash1, hash2);
        Assert.True(PasswordHasher.Verify(password, hash1));
        Assert.True(PasswordHasher.Verify(password, hash2));
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task RateLimitingEnforcesLoginAttempts()
    {
        if (!HasIntegrationConnection()) return;

        Guid? createdUserId = null;
        await using var context = CreateContext();
        try
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = $"ratelimit{Guid.NewGuid():N}@test.com",
                Username = $"ratelimituser{Guid.NewGuid():N}"[..30],
                PasswordHash = PasswordHasher.Hash("Password1!"),
                IsEmailVerified = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            createdUserId = user.Id;

            var rateLimitService = new InMemoryRateLimitService();
            var authService = new AuthService(context, new InMemoryAuditService(), rateLimitService, new ConsoleEmailService());

            const int maxAttempts = 5;
            for (int i = 0; i < maxAttempts; i++)
            {
                await authService.Login(user.Email, "WrongPassword", "127.0.0.1");
            }

            var result = await authService.Login(user.Email, "Password1!", "127.0.0.1");
            Assert.False(result.IsSuccess);
            Assert.Equal(ServiceErrorCode.RateLimited, result.Error?.Code);
        }
        finally
        {
            if (createdUserId.HasValue)
            {
                await CleanupIntegrationUserAsync(context, createdUserId.Value);
            }
        }
    }

    private static async Task CleanupIntegrationUserAsync(AppDbContext context, Guid userId)
    {
        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            return;
        }

        context.Users.Remove(user);
        await context.SaveChangesAsync();
    }

    #endregion
}
