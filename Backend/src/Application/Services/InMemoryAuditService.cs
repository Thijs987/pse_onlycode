using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;

namespace Application.Services;

/// <summary>
/// Simple in-memory audit logger. For production, persist to DB or logging service.
/// </summary>
public class InMemoryAuditService : IAuditService
{
    private readonly List<AuditLog> _logs = new();

    public class AuditLog
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Reason { get; set; }
        public string? IpAddress { get; set; }
    }

    public Task LogAuthEventAsync(string action, string email, bool success, string? reason, string? ipAddress = null)
    {
        var log = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            Email = email,
            Success = success,
            Reason = reason,
            IpAddress = ipAddress
        };

        lock (_logs)
        {
            _logs.Add(log);
        }

        // TODO: In production, write to database or logging service instead
        Log.Information("[AUDIT] Action={Action}, Email={Email}, Success={Success}, Reason={Reason}, IP={IpAddress}",
            log.Action, log.Email, log.Success, log.Reason, log.IpAddress);

        return Task.CompletedTask;
    }
}
