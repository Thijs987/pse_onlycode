using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Services;
using Application;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class AuthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static bool HasIntegrationConnection() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Integration")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Default"));

    [Fact]
    public async Task RegisterEndpoint_GeneratesVerificationLink_AndVerifyEmailEndpoint_CanBeClicked()
    {
        if (!HasIntegrationConnection())
        {
            return;
        }

        var email = $"linktest+{Guid.NewGuid():N}@example.com";
        var username = $"linktest{Guid.NewGuid():N}"[..30];
        var recordingEmail = new RecordingEmailService();

        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("AppSettings:AllowHttp", "true"),
                    new KeyValuePair<string, string?>("AppSettings:BaseUrl", "http://localhost")
                });
            });

            builder.ConfigureServices(services =>
            {
                var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
                if (emailDescriptor != null)
                {
                    services.Remove(emailDescriptor);
                }
                services.AddSingleton<IEmailService>(recordingEmail);
            });
        });

        var client = factory.CreateClient();

        var registerPayload = new
        {
            Email = email,
            Username = username,
            Password = "Password1!"
        };

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerPayload);
        if (!registerResponse.IsSuccessStatusCode)
        {
            var content = await registerResponse.Content.ReadAsStringAsync();
            throw new Exception($"Register failed: {registerResponse.StatusCode} - {content}");
        }
        registerResponse.EnsureSuccessStatusCode();

        Assert.Equal(1, recordingEmail.VerificationEmailCount);
        Assert.False(string.IsNullOrWhiteSpace(recordingEmail.LastLink));
        Assert.Contains("email=", recordingEmail.LastLink!);
        Assert.Contains("token=", recordingEmail.LastLink!);

        var verificationUri = new Uri(recordingEmail.LastLink!);
        var verifyResponse = await client.GetAsync(verificationUri.PathAndQuery);
        if (!verifyResponse.IsSuccessStatusCode)
        {
            var content = await verifyResponse.Content.ReadAsStringAsync();
            throw new Exception($"Verification failed: {verifyResponse.StatusCode} - {content}");
        }
        verifyResponse.EnsureSuccessStatusCode();

        var payload = await verifyResponse.Content.ReadFromJsonAsync<VerifyEmailResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.success);
        Assert.Equal("Email verified successfully", payload.message);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifiedUser = await db.Users.FirstAsync(u => u.Email == email);
        Assert.True(verifiedUser.IsEmailVerified);
        Assert.Null(verifiedUser.VerificationToken);
        Assert.Null(verifiedUser.VerificationTokenExpiry);
    }

    private sealed record VerifyEmailResponse(bool success, string message);

    private sealed class RecordingEmailService : IEmailService
    {
        public int VerificationEmailCount { get; private set; }
        public string? LastTo { get; private set; }
        public string? LastUsername { get; private set; }
        public string? LastToken { get; private set; }
        public string? LastLink { get; private set; }

        public Task SendVerificationEmailAsync(string email, string username, string verificationToken, string verificationLink)
        {
            VerificationEmailCount++;
            LastTo = email;
            LastUsername = username;
            LastToken = verificationToken;
            LastLink = verificationLink;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string email, string username, string resetToken, string resetLink) => Task.CompletedTask;
        public Task SendNotificationEmailAsync(string email, string subject, string body) => Task.CompletedTask;
    }
}
