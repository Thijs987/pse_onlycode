using System;
using System.Text;
using System.Net;
using System.Net.Mail;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Serilog;

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
        // If BaseUrl is not present in provided options, try to read AppSettings:BaseUrl from appsettings.json
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            try
            {
                var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory!, "..", "..", "..", "..", ".."));
                var candidate = Path.Combine(repoRoot, "Backend", "src", "appsettings.json");
                if (!File.Exists(candidate)) candidate = Path.Combine(Directory.GetCurrentDirectory(), "Backend", "src", "appsettings.json");
                if (!File.Exists(candidate)) candidate = Path.Combine("/home/sem/PSE-Green-Code", "Backend", "src", "appsettings.json");
                if (File.Exists(candidate))
                {
                    var json = JsonNode.Parse(File.ReadAllText(candidate));
                    var baseUrl = json?["AppSettings"]?["BaseUrl"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(baseUrl)) _settings.BaseUrl = baseUrl;
                }
            }
            catch
            {
                // ignore
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

            // also try to read AppSettings:BaseUrl if present
            var baseUrl = json?["AppSettings"]? ["BaseUrl"]?.GetValue<string>() ?? string.Empty;

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
                FromEmail = fromEmail,
                BaseUrl = baseUrl
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
            Log.Debug("Generated email Message-ID {MessageId}.", messageId);
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

        // If the passed link looks like a placeholder (example.com) and we have a configured BaseUrl,
        // rebuild the verification link using the configured BaseUrl to ensure the email points at our server.
        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            try
            {
                var uri = new Uri(verificationLink);
                if (uri.Host.Contains("example.com") || string.IsNullOrWhiteSpace(uri.Host))
                {
                    var cleanedBase = _settings.BaseUrl.TrimEnd('/');
                    verificationLink = $"{cleanedBase}/api/auth/verify-email?token={Uri.EscapeDataString(verificationToken)}&email={Uri.EscapeDataString(email)}";
                }
            }
            catch
            {
                // if verificationLink is not a valid URI, fall back to configured base
                var cleanedBase = _settings.BaseUrl.TrimEnd('/');
                verificationLink = $"{cleanedBase}/api/auth/verify-email?token={Uri.EscapeDataString(verificationToken)}&email={Uri.EscapeDataString(email)}";
            }
        }

        var bodyHtml = $"""
        <p>Hello {username},</p>
        <p>Thank you for registering.</p>
        <p>Please verify your email address by clicking the link below:</p>
        <p><a href=\"{verificationLink}\">Verify your email address</a></p>
        <p>If the link above is not clickable, copy and paste the following URL into your browser:</p>
        <p><code>{verificationLink}</code></p>
        <p>If you did not create this account, you can ignore this email.</p>
        <p>Kind regards,<br/>Code Green</p>
        """;

        var plainText = $"""
        Hello {username},

        Thank you for registering.

        Please verify your email address by opening the link below or copying it into your browser:

        {verificationLink}

        If you did not create this account, you can ignore this email.

        Kind regards,
        Code Green
        """;

        using var message = new MailMessage
        {
            From = FromAddress(),
            Subject = "Verify your email address",
            Body = plainText,
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        // Add both text and HTML alternate views so mail clients render correctly and show the raw URL
        var textView = AlternateView.CreateAlternateViewFromString(plainText, Encoding.UTF8, "text/plain");
        var htmlView = AlternateView.CreateAlternateViewFromString(bodyHtml, Encoding.UTF8, "text/html");
        message.AlternateViews.Add(textView);
        message.AlternateViews.Add(htmlView);

        AddMessageIdHeader(message);

        message.To.Add(email);

        try
        {
            Log.Information("Sending verification email to {Email}.", email);
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Email sending failed (verification) to {Email}.", email);
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

        var resetHtml = $"""
        <p>Hello {username},</p>
        <p>A password reset was requested for your account.</p>
        <p>Reset your password here: <a href=\"{resetLink}\">Reset password</a></p>
        <p>If the link above is not clickable, copy and paste the following URL into your browser:</p>
        <p><code>{resetLink}</code></p>
        <p>If you did not request this, please ignore this email.</p>
        <p>Kind regards,<br/>Code Green</p>
        """;

        var resetText = $"""
        Hello {username},

        A password reset was requested for your account.

        Reset your password by opening the link below or copying it into your browser:

        {resetLink}

        If you did not request this, please ignore this email.

        Kind regards,
        Code Green
        """;

        using var message = new MailMessage
        {
            From = FromAddress(),
            Subject = "Password Reset",
            Body = resetText,
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        var resetTextView = AlternateView.CreateAlternateViewFromString(resetText, Encoding.UTF8, "text/plain");
        var resetHtmlView = AlternateView.CreateAlternateViewFromString(resetHtml, Encoding.UTF8, "text/html");
        message.AlternateViews.Add(resetTextView);
        message.AlternateViews.Add(resetHtmlView);

        AddMessageIdHeader(message);

        message.To.Add(email);

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Email sending failed (reset) to {Email}.", email);
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
            Log.Information("Sending notification email to {Email}.", email);
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Email sending failed (notification) to {Email}.", email);
            throw;
        }
    }
}