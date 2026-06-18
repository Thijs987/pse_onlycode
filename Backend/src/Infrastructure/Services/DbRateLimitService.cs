using Application.Services;
using Infrastructure.Persistence;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class DbRateLimitService : IRateLimitService
{
    private readonly AppDbContext _db;

    public DbRateLimitService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsAllowedAsync(string key, int maxAttempts, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        return await _db.Set<RateLimitEntry>()
            .Where(entry => entry.Key == key && entry.AttemptedAt >= cutoff)
            .CountAsync() < maxAttempts;
    }

    public async Task RecordAttemptAsync(string key)
    {
        _db.Add(new RateLimitEntry
        {
            Key = key,
            AttemptedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task ResetAsync(string key)
    {
        var entries = _db.Set<RateLimitEntry>().Where(entry => entry.Key == key);
        _db.RemoveRange(entries);
        await _db.SaveChangesAsync();
    }
}
