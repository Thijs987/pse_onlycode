using Application;
using Infrastructure.Persistence;
using Application.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

// Register services
builder.Services.AddScoped<AuthService>();
// Register email service: prefer explicit env var, then appsettings; default to console stub for safety
var sendRealEnv = Environment.GetEnvironmentVariable("SEND_REAL_EMAIL");
var sendReal = (sendRealEnv ?? string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase) || (sendRealEnv ?? "0") == "1";

// Bind EmailSettings from configuration so we can inspect credentials at startup
var configuredEmailSettings = new EmailSettings();
builder.Configuration.GetSection("EmailSettings").Bind(configuredEmailSettings);

var hasCreds = !string.IsNullOrWhiteSpace(configuredEmailSettings.Username) && !string.IsNullOrWhiteSpace(configuredEmailSettings.Password);
if (!hasCreds)
{
    var fallbackSettings = LoadEmailSettingsFromAppSettings();
    if (fallbackSettings != null)
    {
        configuredEmailSettings = fallbackSettings;
        hasCreds = true;
        Console.WriteLine("EmailSettings loaded from Backend/src/appsettings.json fallback.");
    }
}

if (sendReal || hasCreds)
{
    // Register SMTP implementation and ensure options available
    builder.Services.AddScoped<IEmailService, GmailEmailService>();
    Console.WriteLine("IEmailService: GmailEmailService registered (SMTP will be used).");
}
else
{
    // Default to console stub in dev or when no SMTP settings provided
    builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();
    Console.WriteLine("IEmailService: ConsoleEmailService registered (no SMTP credentials found).");
}
//builder.Services.AddScoped<LobbyService>(); -> Still has to be made

// Audit and rate limit services (in-memory implementations; replace with distributed versions in production)
builder.Services.AddSingleton<Application.Services.IAuditService, Application.Services.InMemoryAuditService>();
builder.Services.AddSingleton<Application.Services.IRateLimitService, Application.Services.InMemoryRateLimitService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<MessageRouter>();

var app = builder.Build();

app.MapControllers();

app.UseWebSockets();

app.Map("/lobby", async (HttpContext context, ConnectionManager connectionManager, MessageRouter router) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        string lobbyId = context.Request.Query["lobbyId"].ToString();
        string playerId = context.Request.Query["playerId"].ToString();

        if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(playerId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!connectionManager.IsLobbyAvailable(lobbyId))
        {
            Console.WriteLine($"Rejected {playerId}: Lobby {lobbyId} is full or doesn't exist.");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        await connectionManager.HandleConnectionAsync(playerId, lobbyId, webSocket, router, context.RequestAborted);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.MapLobbyEndpoints();
app.Run();

static EmailSettings? LoadEmailSettingsFromAppSettings()
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

        var settings = JsonSerializer.Deserialize<EmailSettings>(emailSection.ToJsonString());
        if (settings == null)
        {
            return null;
        }
        if (!string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrWhiteSpace(settings.Password))
        {
            if (string.IsNullOrWhiteSpace(settings.FromEmail))
            {
                settings.FromEmail = settings.Username;
            }
            return settings;
        }
    }
    catch
    {
        // ignore failures and fall back to DI configuration
    }

    return null;
}