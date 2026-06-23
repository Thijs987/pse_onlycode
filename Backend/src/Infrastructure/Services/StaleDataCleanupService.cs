using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class StaleDataCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaleDataCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval;
    private readonly int _refreshTokenRetentionDays;
    private readonly int _rateLimitRetentionHours;

    public StaleDataCleanupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<StaleDataCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cleanupInterval = TimeSpan.FromHours(configuration.GetValue("AppSettings:RefreshTokenCleanupIntervalHours", 6));
        _refreshTokenRetentionDays = configuration.GetValue("AppSettings:RefreshTokenCleanupRetentionDays", 30);
        _rateLimitRetentionHours = configuration.GetValue("AppSettings:RateLimitCleanupRetentionHours", 24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup stale refresh tokens.");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var refreshTokenRemoved = await CleanupExpiredRefreshTokensAsync(context, _refreshTokenRetentionDays, cancellationToken);
        var rateLimitRemoved = await CleanupExpiredRateLimitEntriesAsync(context, _rateLimitRetentionHours, cancellationToken);

        if (refreshTokenRemoved > 0)
        {
            _logger.LogInformation("Removed {Count} stale refresh token(s) from the database.", refreshTokenRemoved);
        }

        if (rateLimitRemoved > 0)
        {
            _logger.LogInformation("Removed {Count} stale rate-limit entry(ies) from the database.", rateLimitRemoved);
        }
    }

    public static async Task<int> CleanupExpiredRefreshTokensAsync(AppDbContext context, int retentionDays, CancellationToken cancellationToken = default)
    {
        if (retentionDays < 0) throw new ArgumentOutOfRangeException(nameof(retentionDays));

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var staleTokens = await context.RefreshTokens
            .Where(rt => (rt.Revoked.HasValue && rt.Revoked.Value <= cutoff)
                       || (rt.Revoked == null && rt.Expires <= cutoff))
            .ToListAsync(cancellationToken);

        if (!staleTokens.Any())
        {
            return 0;
        }

        context.RefreshTokens.RemoveRange(staleTokens);
        await context.SaveChangesAsync(cancellationToken);
        return staleTokens.Count;
    }

    public static async Task<int> CleanupExpiredRateLimitEntriesAsync(AppDbContext context, int retentionHours, CancellationToken cancellationToken = default)
    {
        if (retentionHours < 0) throw new ArgumentOutOfRangeException(nameof(retentionHours));

        var cutoff = DateTime.UtcNow.AddHours(-retentionHours);

        var staleEntries = await context.RateLimitEntries
            .Where(entry => entry.AttemptedAt <= cutoff)
            .ToListAsync(cancellationToken);

        if (!staleEntries.Any())
        {
            return 0;
        }

        context.RateLimitEntries.RemoveRange(staleEntries);
        await context.SaveChangesAsync(cancellationToken);
        return staleEntries.Count;
    }
}
