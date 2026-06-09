using System.Collections.Concurrent;

public static class LobbyEndpoints
{
    public static void MapLobbyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/lobbies");

        // Check if auth enforcement is enabled and if JWT is configured
        var enforceAuth = app.Configuration.GetValue("AppSettings:EnforceAuth", false);
        var authConfigured = !string.IsNullOrWhiteSpace(app.Configuration["Jwt:Key"]);

        // GET /api/lobbies/active
        group.MapGet("/active", (ConnectionManager manager) =>
        {
            var activeLobbies = manager.GetActiveLobbies();
            return Results.Ok(activeLobbies);
        });

        // POST /api/lobbies/create?hostId=Player_x
        var createEndpoint = group.MapPost("/create", (string? hostId, ConnectionManager manager) =>
        {
            // Basic input validation
            if (string.IsNullOrWhiteSpace(hostId))
            {
                // HostId is optional, but require non-empty if provided
                hostId = "";
            }

            string newLobbyId = manager.CreateLobby();
            return Results.Ok(new { LobbyId = newLobbyId });
        });

        // If enforcement is enabled and auth is configured, require authorization for lobby creation
        if (enforceAuth && authConfigured)
        {
            createEndpoint.RequireAuthorization();
        }
    }
}