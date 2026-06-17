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
        group.MapGet("/active", (ConnectionManager manager, MatchManager matchManager) =>
        {
            var activeLobbies = manager.GetActiveLobbies(matchManager);
            return Results.Ok(activeLobbies);
        });

        // POST /api/lobbies/create?hostId=Player_x
        var createEndpoint = group.MapPost("/create", (string? hostId, ConnectionManager manager) =>
        {
            string newLobbyId = manager.CreateLobby(hostId);
            return Results.Ok(new { LobbyId = newLobbyId });
        });

        // If enforcement is enabled and auth is configured, require authorization for lobby creation
        if (enforceAuth && authConfigured)
        {
            createEndpoint.RequireAuthorization();
        }
    }
}