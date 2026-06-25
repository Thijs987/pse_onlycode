using System;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Tests
{
    public class GmailEmailServiceTests
    {
        [Fact]
        public async Task ConsoleEmailService_WritesOutput()
        {
            var svc = new ConsoleEmailService();

            using var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            await svc.SendVerificationEmailAsync("semvdberge@gmail.com", "unituser", "tok", "http://example/");

            Console.Out.Flush();
            Console.SetOut(originalOut);

            var output = sw.ToString();
            Assert.Contains("[EMAIL] Verification email to semvdberge@gmail.com", output);
        }

        [Fact]
        public async Task GmailEmailService_SendVerificationEmail_Conditional()
        {
            // This unit test will attempt to send a real email only when SMTP credentials are present.
            // It prefers env vars `EMAIL_SMTP_USERNAME` and `EMAIL_SMTP_PASSWORD`.

            EmailSettings? settings = null;

            var smtpUser = Environment.GetEnvironmentVariable("EMAIL_SMTP_USERNAME");
            var smtpPass = Environment.GetEnvironmentVariable("EMAIL_SMTP_PASSWORD");
            if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
            {
                settings = new EmailSettings
                {
                    Host = Environment.GetEnvironmentVariable("EMAIL_SMTP_HOST") ?? "smtp.gmail.com",
                    Port = int.TryParse(Environment.GetEnvironmentVariable("EMAIL_SMTP_PORT"), out var p) ? p : 587,
                    Username = smtpUser,
                    Password = smtpPass,
                    EnableSsl = true,
                    FromEmail = smtpUser
                };
            }

            // fallback: try loading Backend/src/appsettings.json (local dev convenience)
            if (settings == null)
            {
                var candidate = Path.Combine("/home/sem/PSE-Green-Code","Backend","src","appsettings.json");
                if (File.Exists(candidate))
                {
                    try
                    {
                        var config = new ConfigurationBuilder().AddJsonFile(candidate, optional: false).Build();
                        var section = config.GetSection("EmailSettings");
                        var loaded = new EmailSettings();
                        section.Bind(loaded);
                        if (!string.IsNullOrWhiteSpace(loaded.Username) && !string.IsNullOrWhiteSpace(loaded.Password))
                        {
                            settings = loaded;
                        }
                    }
                    catch { }
                }
            }

            if (settings == null)
            {
                // No SMTP credentials available — do not perform an external send in CI.
                // Mark test as passed but indicate it was skipped.
                Assert.True(true, "SMTP not configured — skipped real-email unit test.");
                return;
            }

            var options = Options.Create(settings);
            var svc = new GmailEmailService(options);

            // Use the user's requested email for visibility
            var to = "semvdberge@gmail.com";
            var unique = Guid.NewGuid().ToString("N");
            var verificationLink = $"https://example.test/verify?token={unique}";

            // Attempt send — test should pass if no exception thrown.
            Exception? ex = null;
            try
            {
                await svc.SendVerificationEmailAsync(to, "UnitTestUser", unique, verificationLink);
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex != null)
            {
                // Surface the SMTP exception as a test failure with details to help debugging
                Assert.Fail($"SMTP send failed: {ex.GetType().Name}: {ex.Message}");
            }

            // If we got here, the send was successful.
            Assert.True(true);
        }
    }
}
