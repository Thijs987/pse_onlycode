using System.Threading.Tasks;
using Serilog;

namespace Application.Services;

/// <summary>
/// Stub email service that logs to console. Replace with real implementation in production.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    public Task SendVerificationEmailAsync(string email, string username, string verificationToken, string verificationLink)
    {
        Log.Information("[EMAIL] Verification email to {Email}. Username={Username}, Token={Token}, Link={Link}",
            email, username, verificationToken, verificationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string email, string username, string resetToken, string resetLink)
    {
        Log.Information("[EMAIL] Password reset email to {Email}. Token={Token}, Link={Link}",
            email, resetToken, resetLink);
        return Task.CompletedTask;
    }

    public Task SendNotificationEmailAsync(string email, string subject, string body)
    {
        Log.Information("[EMAIL] Notification to {Email}. Subject={Subject}, Body={Body}",
            email, subject, body);
        return Task.CompletedTask;
    }
}
