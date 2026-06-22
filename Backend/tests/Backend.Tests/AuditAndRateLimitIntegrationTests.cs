using System;
using System.Linq;
using System.Threading.Tasks;
using Application.Services;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class AuditAndRateLimitIntegrationTests
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
    public async Task AuditEvent_IsPersistedToDatabase_Integration()
    {
        if (!HasIntegrationConnection())
        {
            return;
        }

        var email = $"test+{Guid.NewGuid():N}@example.com";
        var reason = "Integration test audit event";
        Guid? auditId = null;

        await using var context = CreateContext();
        var auditService = new DbAuditService(context);

        try
        {
            await auditService.LogAuthEventAsync("LoginAttempt", email, success: false, reason, ipAddress: "127.0.0.1");

            var persisted = await context.AuditLogs
                .Where(x => x.Action == "LoginAttempt" && x.Email == email && x.Reason == reason)
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefaultAsync();

            Assert.NotNull(persisted);
            Assert.Equal("LoginAttempt", persisted!.Action);
            Assert.Equal(email, persisted.Email);
            Assert.False(persisted.Success);
            Assert.Equal(reason, persisted.Reason);
            Assert.Equal("127.0.0.1", persisted.IpAddress);

            auditId = persisted.Id;
        }
        finally
        {
            if (auditId.HasValue)
            {
                var auditLog = await context.AuditLogs.FindAsync(auditId.Value);
                if (auditLog != null)
                {
                    context.AuditLogs.Remove(auditLog);
                    await context.SaveChangesAsync();
                }
            }
        }
    }

    [Fact]
    public async Task RateLimitService_TracksAttemptsAndEnforcesLimit_Integration()
    {
        if (!HasIntegrationConnection())
        {
            return;
        }

        var key = $"rl-{Guid.NewGuid():N}";
        const int maxAttempts = 3;
        var window = TimeSpan.FromMinutes(1);

        await using var context = CreateContext();
        var rateLimitService = new DbRateLimitService(context);

        var existingEntries = context.RateLimitEntries.Where(x => x.Key == key);
        context.RemoveRange(existingEntries);
        await context.SaveChangesAsync();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Assert.True(await rateLimitService.IsAllowedAsync(key, maxAttempts, window));
            await rateLimitService.RecordAttemptAsync(key);
        }

        Assert.False(await rateLimitService.IsAllowedAsync(key, maxAttempts, window));

        var storedEntries = await context.RateLimitEntries.Where(x => x.Key == key).ToListAsync();
        Assert.Equal(maxAttempts, storedEntries.Count);

        await rateLimitService.ResetAsync(key);

        Assert.True(await rateLimitService.IsAllowedAsync(key, maxAttempts, window));
        Assert.Empty(await context.RateLimitEntries.Where(x => x.Key == key).ToListAsync());
    }
}
