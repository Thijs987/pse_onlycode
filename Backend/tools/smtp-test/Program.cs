using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;

Console.WriteLine("SMTP test tool — reading Backend/src/appsettings.json and sending one email to semvdberge@gmail.com");

var envUsername = Environment.GetEnvironmentVariable("EMAIL_SMTP_USERNAME");
var envPassword = Environment.GetEnvironmentVariable("EMAIL_SMTP_PASSWORD");
string host;
int port;
string username;
string password;
string fromEmail;
bool enableSsl;

if (!string.IsNullOrWhiteSpace(envUsername) && !string.IsNullOrWhiteSpace(envPassword))
{
    host = Environment.GetEnvironmentVariable("EMAIL_SMTP_HOST") ?? "smtp.gmail.com";
    port = int.TryParse(Environment.GetEnvironmentVariable("EMAIL_SMTP_PORT"), out var envPort) ? envPort : 587;
    username = envUsername;
    password = envPassword;
    fromEmail = Environment.GetEnvironmentVariable("EMAIL_SMTP_FROM") ?? envUsername;
    enableSsl = bool.TryParse(Environment.GetEnvironmentVariable("EMAIL_SMTP_ENABLESSL"), out var envSsl) ? envSsl : true;
    Console.WriteLine("Loaded SMTP settings from environment variables.");
}
else
{
    var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
    var candidate = Path.Combine(repoRoot, "Backend", "src", "appsettings.json");
    if (!File.Exists(candidate))
    {
        candidate = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "src", "appsettings.json");
    }

    if (!File.Exists(candidate))
    {
        Console.WriteLine("Could not find appsettings.json at expected locations.");
        Console.WriteLine("Checked:");
        Console.WriteLine(Path.Combine(repoRoot, "Backend", "src", "appsettings.json"));
        Console.WriteLine(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "src", "appsettings.json"));
        return 1;
    }

    Console.WriteLine($"Found appsettings.json: {candidate}");

    var json = JsonNode.Parse(File.ReadAllText(candidate));
    var emailSection = json?["EmailSettings"] as JsonObject;
    if (emailSection == null)
    {
        Console.WriteLine("EmailSettings section not found in appsettings.json");
        return 1;
    }

    host = emailSection["Host"]?.GetValue<string>() ?? "smtp.gmail.com";
    port = emailSection["Port"]?.GetValue<int?>() ?? 587;
    username = emailSection["Username"]?.GetValue<string>() ?? string.Empty;
    password = emailSection["Password"]?.GetValue<string>() ?? string.Empty;
    fromEmail = emailSection["FromEmail"]?.GetValue<string>() ?? username;
    enableSsl = emailSection["EnableSsl"]?.GetValue<bool?>() ?? true;
}

Console.WriteLine($"SMTP host={host} port={port} user={username} from={fromEmail} ssl={enableSsl}");

if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
{
    Console.WriteLine("Username or Password missing in SMTP settings — aborting.");
    return 1;
}

using var client = new SmtpClient(host, port)
{
    EnableSsl = enableSsl,
    UseDefaultCredentials = false,
    Credentials = new NetworkCredential(username, password),
    DeliveryMethod = SmtpDeliveryMethod.Network
};

var to = "semvdberge@gmail.com";
var unique = Guid.NewGuid().ToString("N");
var subject = $"Code Green SMTP test — id:{unique}";
var body = $"SMTP test sent at {DateTime.UtcNow:O} from {username} to {to}\n\nid:{unique}";
using var message = new MailMessage(new MailAddress(fromEmail, "Code Green Test"), new MailAddress(to))
{
    Subject = subject,
    Body = body
};

// Add a Message-ID header so we can more easily search for the message if the server preserves it
try
{
    var messageId = $"<{unique}@codegreen.local>";
    message.Headers.Add("Message-ID", messageId);
    Console.WriteLine($"Message-ID: {messageId}");
}
catch
{
    // some transports may ignore or forbid setting Message-ID; ignore failures
}

Console.WriteLine($"Subject: {subject}");

try
{
    Console.WriteLine("Attempting to send email...");
    await client.SendMailAsync(message);
    Console.WriteLine("Email sent successfully.");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine("Email send failed:");
    Console.WriteLine(ex.ToString());
    return 2;
}
return 0;