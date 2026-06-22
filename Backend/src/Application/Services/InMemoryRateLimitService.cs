using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Services;

/// <summary>
/// In-memory rate limiter with sliding window expiry.
/// For production, use distributed cache (Redis) instead.
/// </summary>
public class InMemoryRateLimitService : IRateLimitService
{
    private class RateLimitEntry
    {
        public Queue<DateTime> Attempts { get; } = new();
    }

    private readonly Dictionary<string, RateLimitEntry> _store = new();
    private readonly object _lock = new();

    public Task<bool> IsAllowedAsync(string key, int maxAttempts, TimeSpan window)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            if (!_store.TryGetValue(key, out var entry))
            {
                return Task.FromResult(true);
            }

            // Remove old attempts outside the window
            while (entry.Attempts.Count > 0 && now - entry.Attempts.Peek() > window)
            {
                entry.Attempts.Dequeue();
            }

            // Check if over limit
            var allowed = entry.Attempts.Count < maxAttempts;
            return Task.FromResult(allowed);
        }
    }

    public Task RecordAttemptAsync(string key)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            if (!_store.TryGetValue(key, out var entry))
            {
                entry = new RateLimitEntry();
                _store[key] = entry;
            }

            entry.Attempts.Enqueue(now);
        }

        return Task.CompletedTask;
    }

    public Task ResetAsync(string key)
    {
        lock (_lock)
        {
            _store.Remove(key);
        }

        return Task.CompletedTask;
    }
}
