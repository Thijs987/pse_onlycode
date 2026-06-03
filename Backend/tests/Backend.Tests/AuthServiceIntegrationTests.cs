using System;
using System.Threading.Tasks;
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

        var email = $"integration-{Guid.NewGuid():N}@example.com";
        var username = $"intuser{Guid.NewGuid():N}";
        if (username.Length > 30)
        {
            username = username.Substring(0, 30);
        }

        await using var context = CreateContext();
        var service = new AuthService(
            context,
            new InMemoryAuditService(),
            new InMemoryRateLimitService(),
            new ConsoleEmailService());

        var registerResult = await service.Register(email, username, "Password1!", "127.0.0.1");
        Assert.True(registerResult.IsSuccess);
        Assert.Equal(email, registerResult.Value.Email);
        Assert.Equal(username, registerResult.Value.Username);

        var loginResult = await service.Login(email, "Password1!", "127.0.0.1");
        Assert.True(loginResult.IsSuccess);
        Assert.Equal(email, loginResult.Value.Email);

        var createdUser = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (createdUser != null)
        {
            context.Users.Remove(createdUser);
            await context.SaveChangesAsync();
        }
    }
}
