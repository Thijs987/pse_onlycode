using Application.Services;
using Infrastructure.Persistence;
using Domain;
using Serilog;

namespace Infrastructure.Services;

public class DbAuditService : IAuditService
{
    private readonly AppDbContext _db;

    public DbAuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAuthEventAsync(string action, string email, bool success, string? reason, string? ipAddress = null)
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

        _db.Add(log);
        await _db.SaveChangesAsync();

        Log.Information("[AUDIT] Action={Action}, Email={Email}, Success={Success}, Reason={Reason}, IP={IpAddress}",
            log.Action, log.Email, log.Success, log.Reason, log.IpAddress);
    }
}
