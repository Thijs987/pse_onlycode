using System;
using System.Threading.Tasks;

namespace Application.Services;

/// <summary>
/// Rate limiting service: tracks attempts per IP/email and enforces limits.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Check if an action is rate-limited. Returns true if allowed, false if exceeded limit.
    /// </summary>
    Task<bool> IsAllowedAsync(string key, int maxAttempts, TimeSpan window);

    /// <summary>
    /// Record an attempt for a given key.
    /// </summary>
    Task RecordAttemptAsync(string key);

    /// <summary>
    /// Reset attempts for a key (like after successful login).
    /// </summary>
    Task ResetAsync(string key);
}
