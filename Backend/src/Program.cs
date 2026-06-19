using System;
using Application;
using Application.Results;
using Application.Interfaces;
using Infrastructure.Persistence;
using Application.Services;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Reflection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Api;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

var maxRequestBodySizeBytes = builder.Configuration.GetValue<long?>("AppSettings:MaxRequestBodySizeBytes") ?? 10L * 1024 * 1024;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBodySizeBytes;
});

builder.Host.UseSerilog();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidateModelAttribute>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.MaxDepth = 32;
    options.JsonSerializerOptions.DefaultBufferSize = 4096;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.AllowTrailingCommas = false;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.MaxDepth = 32;
    options.SerializerOptions.DefaultBufferSize = 4096;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.AllowTrailingCommas = false;
});

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

// Database context: PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Configure EmailSettings from configuration
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

// Register services
builder.Services.AddScoped<AuthService>();
// Register email service: prefer SMTP when credentials are available; otherwise use a console stub

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
        Log.Information("EmailSettings loaded from Backend/src/appsettings.json fallback.");
    }
}

if (hasCreds)
{
    builder.Services.AddSingleton<IEmailService, GmailEmailService>();
    Log.Information("IEmailService: GmailEmailService registered (SMTP will be used).");
}
else
{
    // Default to console stub in dev or when no SMTP settings provided
    builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();
    Log.Information("IEmailService: ConsoleEmailService registered (no SMTP credentials found).");
}
//builder.Services.AddScoped<LobbyService>(); -> Still has to be made

// Audit and rate limit services: use persistent database-backed implementations in production
builder.Services.AddScoped<IAuditService, DbAuditService>();
builder.Services.AddScoped<IRateLimitService, DbRateLimitService>();
var cardTypes = typeof(ICardEffect).Assembly.GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract && typeof(ICardEffect).IsAssignableFrom(t));

foreach (var type in cardTypes)
{
    builder.Services.AddSingleton(typeof(ICardEffect), type);
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<MatchManager>();
builder.Services.AddSingleton<BotService>();

// trust X-Forwarded-* headers when behind a reverse proxy (controlled by config)
// This is important for correct client IP logging and HTTPS detection when behind a proxy 
// like Nginx or Traefik.
var forwardedHeadersEnabled = builder.Configuration.GetValue("AppSettings:TrustForwardedHeaders", false);
if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Add(System.Net.IPAddress.Loopback);
        options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
        options.ForwardLimit = 1;
    });
}

// Authentication with JWT bearer tokens (if configured)
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PSE-Green";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PSE-Green-Clients";
var enforceAuth = builder.Configuration.GetValue("AppSettings:EnforceAuth", false) ||
                  (Environment.GetEnvironmentVariable("ENFORCE_AUTH") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);

var authEnabled = !string.IsNullOrWhiteSpace(jwtKey);
if (authEnabled)
{
    // Configure JWT bearer authentication
    var keyBytes = Encoding.UTF8.GetBytes(jwtKey!);

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long for secure HMAC-SHA256 signing.");
        }

        // In production, require JWT authentication if a key is provided or if EnforceAuth is true.
        // In development, allow unauthenticated access if no key is configured for ease of testing.
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // Configure JWT validation parameters
            // In production, require HTTPS for JWT tokens (enforced via environment)
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                // Validate the JWT token parameters
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidateLifetime = true,
                // Small clock skew to allow for minor clock differences between clients and server
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            // Prefer Authorization header and cookie; do not accept access_token values from query string.
            options.Events = new JwtBearerEvents
            {
                // This allows JWT tokens to be sent in the Authorization header
                OnMessageReceived = context =>
                {
                    var authorizationHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Token = authorizationHeader[7..].Trim();
                        return Task.CompletedTask;
                    }

                    var accessToken = context.Request.Cookies["access_token"];
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
    });

    Log.Information("Authentication: JWT bearer registered.");
}

var app = builder.Build();

if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
}

app.UseSerilogRequestLogging();

app.Use(async (context, next) =>
{
    if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > maxRequestBodySizeBytes)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await context.Response.WriteAsync("Request body too large.");
        return;
    }

    var maxFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (maxFeature != null && !maxFeature.IsReadOnly)
    {
        maxFeature.MaxRequestBodySize = maxRequestBodySizeBytes;
    }

    await next();
});

// Security headers middleware: defense-in-depth for API and any served frontend
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    if (!headers.ContainsKey("X-Content-Type-Options"))
        headers["X-Content-Type-Options"] = "nosniff";

    if (!headers.ContainsKey("X-Frame-Options"))
        headers["X-Frame-Options"] = "DENY";

    if (!headers.ContainsKey("Referrer-Policy"))
        headers["Referrer-Policy"] = "no-referrer";

    if (!headers.ContainsKey("Content-Security-Policy"))
    {
        // Conservative default CSP for API-only servers; adjust for frontend needs.
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self';";
    }

    await next();
});

// Apply CORS policy if configured
if (allowedOrigins != null && allowedOrigins.Length > 0)
{
    app.UseCors();
}

// Security middleware
if (!app.Environment.IsDevelopment())
{
    // HSTS: Tells browsers to always use HTTPS (max-age: 1 year)
    app.UseHsts();
}

// Redirect HTTP to HTTPS unless explicitly allowed (controlled by AllowHttp config)
var allowHttp = builder.Configuration.GetValue("AppSettings:AllowHttp", false);
if (!allowHttp)
{
    app.UseHttpsRedirection();
}

// Rate limiting middleware (uses IRateLimitService) - enabled via config
var rateLimitEnabled = builder.Configuration.GetValue("AppSettings:RateLimit:Enabled", false);
if (rateLimitEnabled)
{
    app.Use(async (context, next) =>
    {
        // Check if the request is allowed based on IP and configured limits
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

// Double-submit CSRF protection middleware for state-changing requests.
// Exempt login/register/verify-email endpoints so clients can obtain initial CSRF token.
app.Use(async (context, next) =>
{
    // Skip CSRF check for safe methods (GET, HEAD, OPTIONS)
    var method = context.Request.Method;
    if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
    {
        await next();
        return;
    }

    var path = context.Request.Path.Value ?? string.Empty;
    // Public endpoints that should be exempt from CSRF (they create the session/token)
    if (path.StartsWith("/api/auth/login") || path.StartsWith("/api/auth/register") || path.StartsWith("/api/auth/verify-email"))
    {
        await next();
        return;
    }

    // Check for CSRF token in cookie and header
    var csrfCookie = context.Request.Cookies["csrf_token"];
    var csrfHeader = context.Request.Headers["X-CSRF-Token"].FirstOrDefault();

    // If either is missing or they don't match, reject the request
    if (string.IsNullOrWhiteSpace(csrfCookie) || string.IsNullOrWhiteSpace(csrfHeader) || !string.Equals(csrfCookie, csrfHeader))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Invalid or missing CSRF token");
        return;
    }

    await next();
});

app.MapControllers();
// Map minimal auth endpoints (email verification)
app.MapAuthEndpoints();

app.UseWebSockets();

app.Map("/lobby", async (HttpContext context, ConnectionManager connectionManager, MessageRouter router, MatchManager matchManager) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        // Require authentication for WebSocket access when JWT auth is enabled.
        if (authEnabled && !(context.User?.Identity?.IsAuthenticated == true))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        string lobbyId = context.Request.Query["lobbyId"].ToString();
        string playerId = context.Request.Query["playerId"].ToString();

        if (authEnabled)
        {
            // Use the authenticated subject claim as the authoritative player identifier when available.
            var authenticatedPlayerId = context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (!string.IsNullOrEmpty(authenticatedPlayerId))
            {
                playerId = authenticatedPlayerId;
            }
        }

        if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(playerId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!connectionManager.IsLobbyAvailable(lobbyId))
        {
            Log.Warning("Rejected {PlayerId}: Lobby {LobbyId} is full or doesn't exist.", playerId, lobbyId);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        await connectionManager.HandleConnectionAsync(playerId, lobbyId, webSocket, router, matchManager, context.RequestAborted);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.MapLobbyEndpoints();

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception terminating host");
}
finally
{
    Log.CloseAndFlush();
}

static EmailSettings? LoadEmailSettingsFromAppSettings()
{
    try
    {
        // Attempt to load EmailSettings from Backend/src/appsettings.json (fallback for us)
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory!, "..", "..", "..", "..", ".."));
        var candidate = Path.Combine(repoRoot, "Backend", "src", "appsettings.json");
        if (!File.Exists(candidate))
        {
            candidate = Path.Combine(Directory.GetCurrentDirectory(), "Backend", "src", "appsettings.json");
        }
        if (!File.Exists(candidate))
        {
            return null;
        }

        // Read and parse the appsettings.json file
        var json = JsonNode.Parse(File.ReadAllText(candidate));
        var emailSection = json?["EmailSettings"] as JsonObject;
        if (emailSection == null)
        {
            return null;
        }

        // Deserialize the EmailSettings section into an EmailSettings object
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

public partial class Program { }
