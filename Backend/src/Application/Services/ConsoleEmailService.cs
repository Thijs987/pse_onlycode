using System;
using System.Threading.Tasks;

namespace Application.Services;

/// <summary>
/// Stub email service that logs to console. Replace with real implementation in production.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    public Task SendVerificationEmailAsync(string email, string username, string verificationToken, string verificationLink)
    {
        Console.WriteLine($"[EMAIL] Verification email to {email}");
        Console.WriteLine($"  Username: {username}");
        Console.WriteLine($"  Token: {verificationToken}");
        Console.WriteLine($"  Link: {verificationLink}");
        Console.WriteLine();
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string email, string username, string resetToken, string resetLink)
    {
        Console.WriteLine($"[EMAIL] Password reset email to {email}");
        Console.WriteLine($"  Token: {resetToken}");
        Console.WriteLine($"  Link: {resetLink}");
        Console.WriteLine();
        return Task.CompletedTask;
    }

    public Task SendNotificationEmailAsync(string email, string subject, string body)
    {
        Console.WriteLine($"[EMAIL] Notification to {email}");
        Console.WriteLine($"  Subject: {subject}");
        Console.WriteLine($"  Body: {body}");
        Console.WriteLine();
        return Task.CompletedTask;
    }
}
