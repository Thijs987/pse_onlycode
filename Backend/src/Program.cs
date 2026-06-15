using Application;
using Application.Results;
using Infrastructure.Persistence;
using Application.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// CORS: allow only configured origins
var allowedOrigins = builder.Configuration.GetSection("AppSettings:AllowedOrigins").Get<string[]>();
if (allowedOrigins != null && allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
}

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


// Authentication - optional and driven by configuration. Add JWT bearer if key is configured.
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PSE-Green";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PSE-Green-Clients";
var enforceAuth = builder.Configuration.GetValue("AppSettings:EnforceAuth", false) ||
                  (Environment.GetEnvironmentVariable("ENFORCE_AUTH") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);

var authEnabled = !string.IsNullOrWhiteSpace(jwtKey);
if (authEnabled)
{
    var keyBytes = Encoding.UTF8.GetBytes(jwtKey!);
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateLifetime = true
        };

        // Allow JWT in query string for WebSocket connections (only if present)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Prefer token from query string (WebSocket fallback), otherwise read from cookie named 'access_token'
                var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                if (string.IsNullOrEmpty(accessToken))
                {
                    accessToken = context.Request.Cookies["access_token"];
                }

                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

    Console.WriteLine("Authentication: JWT bearer registered.");
}

var app = builder.Build();

app.MapControllers();

// Apply CORS policy if configured
if (allowedOrigins != null && allowedOrigins.Length > 0)
{
    app.UseCors();
}

// Security middleware
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

// Rate limiting middleware (uses IRateLimitService) - enabled via config
var rateLimitEnabled = builder.Configuration.GetValue("AppSettings:RateLimit:Enabled", false);
if (rateLimitEnabled)
{
    app.Use(async (context, next) =>
    {
        var rl = context.RequestServices.GetRequiredService<Application.Services.IRateLimitService>();
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var max = builder.Configuration.GetValue("AppSettings:RateLimit:Requests", 60);
        var windowSec = builder.Configuration.GetValue("AppSettings:RateLimit:WindowSeconds", 60);

        var allowed = await rl.IsAllowedAsync(ip, max, TimeSpan.FromSeconds(windowSec));
        await rl.RecordAttemptAsync(ip);
        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Too many requests");
            return;
        }

        await next();
    });
}

// Authentication & Authorization middleware
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// Map minimal auth endpoints (email verification)
app.MapAuthEndpoints();

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