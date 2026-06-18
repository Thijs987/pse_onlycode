using System;
using System.Threading.Tasks;

namespace Application.Services;

/// <summary>
/// Email service abstraction for sending transactional emails.
/// Implement this with your email provider (SendGrid, SMTP, etc.).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send an email verification message.
    /// </summary>
    /// <param name="email">Recipient email address</param>
    /// <param name="username">User's username for personalization</param>
    /// <param name="verificationToken">The verification token (to be used in URL)</param>
    /// <param name="verificationLink">Full URL for user to click (e.g., https://yourdomain.com/verify?token=XXX&email=YYY)</param>
    Task SendVerificationEmailAsync(string email, string username, string verificationToken, string verificationLink);

    /// <summary>
    /// Send a password reset email (future use).
    /// </summary>
    Task SendPasswordResetEmailAsync(string email, string username, string resetToken, string resetLink);

    /// <summary>
    /// Send a notification email (e.g., suspicious login detected).
    /// </summary>
    Task SendNotificationEmailAsync(string email, string subject, string body);
}
