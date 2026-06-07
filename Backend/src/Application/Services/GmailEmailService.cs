using System;
using System.Net;
using System.Net.Mail;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class GmailEmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public GmailEmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
        if (!HasValidSmtpSettings(_settings))
        {
            var fallback = LoadSettingsFromAppSettings();
            if (fallback != null)
            {
                _settings = fallback;
            }
        }
    }

    private static bool HasValidSmtpSettings(EmailSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.Username)
            && !string.IsNullOrWhiteSpace(settings.Password);
    }

    private static EmailSettings? LoadSettingsFromAppSettings()
    {
        try
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory!, "..", "..", "..", "..", ".."));
            var candidate = Path.Combine(repoRoot, "Backend", "src", "appsettings.json");
            if (!File.Exists(candidate))
            {
                candidate = Path.Combine(Directory.GetCurrentDirectory(), "Backend", "src", "appsettings.json");
            }
            if (!File.Exists(candidate))
            {
                candidate = Path.Combine("/home/sem/PSE-Green-Code", "Backend", "src", "appsettings.json");
            }
            if (!File.Exists(candidate))
            {
                return null;
            }

            var json = JsonNode.Parse(File.ReadAllText(candidate));
            var emailSection = json?["EmailSettings"] as JsonObject;
            if (emailSection == null)
            {
                return null;
            }

            string host = emailSection["Host"]?.GetValue<string>() ?? "smtp.gmail.com";
            int port = emailSection["Port"]?.GetValue<int?>() ?? 587;
            string username = emailSection["Username"]?.GetValue<string>() ?? string.Empty;
            string password = emailSection["Password"]?.GetValue<string>() ?? string.Empty;
            string fromEmail = emailSection["FromEmail"]?.GetValue<string>() ?? username;
            bool enableSsl = emailSection["EnableSsl"]?.GetValue<bool?>() ?? true;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            return new EmailSettings
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                EnableSsl = enableSsl,
                FromEmail = fromEmail
            };
        }
        catch
        {
            return null;
        }
    }

    private SmtpClient CreateClient()
    {
        var host = string.IsNullOrWhiteSpace(_settings.Host) ? "smtp.gmail.com" : _settings.Host;
        var port = _settings.Port != 0 ? _settings.Port : 587;

        var client = new SmtpClient(host, port)
        {
            EnableSsl = _settings.EnableSsl,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(
                _settings.Username,
                _settings.Password
            )
        };

        return client;
    }

    private MailAddress FromAddress()
    {
        var fromEmail = !string.IsNullOrWhiteSpace(_settings.FromEmail)
            ? _settings.FromEmail
            : _settings.Username;
        return new MailAddress(
            fromEmail,
            "Code Green"
        );
    }

    private static void AddMessageIdHeader(MailMessage message)
    {
        try
        {
            var messageId = $"<{Guid.NewGuid():N}@codegreen.local>";
            message.Headers.Add("Message-ID", messageId);
            Console.WriteLine($"Message-ID: {messageId}");
        }
        catch
        {
            // Some SMTP transports may ignore or forbid Message-ID header modifications.
        }
    }

    public async Task SendVerificationEmailAsync(
        string email,
        string username,
        string verificationToken,
        string verificationLink)
    {
        using var client = CreateClient();

        var body = $"""
        Hello {username},

        Thank you for registering.

        Please verify your email address by clicking the link below:

        {verificationLink}

        If you did not create this account, you can ignore this email.

        Kind regards,
        Code Green
        """;

        using var message = new MailMessage
        {
            From = FromAddress(),
            Subject = "Verify your email address",
            Body = body,
            IsBodyHtml = false
        };

        AddMessageIdHeader(message);

        message.To.Add(email);

        try
        {
            Console.WriteLine("Sending email to: " + email);
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Email sending failed (verification):");
            Console.WriteLine(ex.ToString());
            throw;
        }
    }

    public async Task SendPasswordResetEmailAsync(
        string email,
        string username,
        string resetToken,
        string resetLink)
    {
        using var client = CreateClient();

        var body = $"""
        Hello {username},

        A password reset was requested for your account.

        Reset your password here:

        {resetLink}

        If you did not request this, please ignore this email.

        Kind regards,
        Code Green
        """;

        using var message = new MailMessage
        {
            From = FromAddress(),
            Subject = "Password Reset",
            Body = body,
            IsBodyHtml = false
        };

        AddMessageIdHeader(message);

        message.To.Add(email);

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Email sending failed (reset):");
            Console.WriteLine(ex.ToString());
            throw;
        }
    }

    public async Task SendNotificationEmailAsync(
        string email,
        string subject,
        string body)
    {
        using var client = CreateClient();

        using var message = new MailMessage
        {
            From = FromAddress(),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        AddMessageIdHeader(message);

        message.To.Add(email);

        try
        {
            Console.WriteLine("Sending email to: " + email);
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Email sending failed (notification):");
            Console.WriteLine(ex.ToString());
            throw;
        }
    }
}