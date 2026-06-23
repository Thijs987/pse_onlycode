using System;
using Microsoft.EntityFrameworkCore;
using Application;
using Application.Services;
using Infrastructure.Persistence;

class Program
{
    static async System.Threading.Tasks.Task Main(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("pwreset_test_db")
            .Options;

        using var db = new AppDbContext(options);
        var audit = new InMemoryAuditService();
        var rate = new InMemoryRateLimitService();
        var email = new ConsoleEmailService();

        var svc = new AuthService(db, audit, rate, email);

        var emailAddr = "pwtest@example.com";
        var username = "pwtestuser";
        var pwd = "OldP@ssword1";

        var reg = await svc.Register(emailAddr, username, pwd, "127.0.0.1");
        Console.WriteLine($"Register: Success={reg.IsSuccess} Error={reg.Error?.Message}");

        var req = await svc.RequestPasswordResetAsync(emailAddr);
        Console.WriteLine($"RequestPasswordResetAsync: Success={req.IsSuccess} ErrorCode={req.Error?.Code} ErrorMsg={req.Error?.Message}");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailAddr);
        Console.WriteLine($"User token hash present: {user?.PasswordResetToken != null}, expiry: {user?.PasswordResetTokenExpiry}");
    }
}
