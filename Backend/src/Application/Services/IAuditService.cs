using System;
using System.Threading.Tasks;

namespace Application.Services;

/// <summary>
/// Audit logging service for authentication and security events.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Log an authentication event (login/register attempt).
    /// </summary>
    Task LogAuthEventAsync(string action, string email, bool success, string? reason, string? ipAddress = null);
}
