using System.Collections.Concurrent;

public static class LobbyEndpoints
{
    public static void MapLobbyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/lobbies").RequireJwtAuthentication(app.Configuration);

        // GET /api/lobbies/active
        group.MapGet("/active", (ConnectionManager manager, MatchManager matchManager) =>
        {
            var activeLobbies = manager.GetActiveLobbies(matchManager);
            return Results.Ok(activeLobbies);
        });

        group.MapGet("/rejoin", (ConnectionManager manager, MatchManager matchManager, string playerId) =>
        {
            var rejoinLobbies = manager.RejoinLobbies(playerId, matchManager);
            return Results.Ok(rejoinLobbies);
        });

        // POST /api/lobbies/create?hostId=Player_x
        group.MapPost("/create", (HttpContext context, string? hostId, ConnectionManager manager) =>
        {
            var username = context.User.FindFirst("username")?.Value;
            if (!string.IsNullOrEmpty(username))
            {
                hostId = username;
            }
            string newLobbyId = manager.CreateLobby(hostId);
            return Results.Ok(new { LobbyId = newLobbyId });
        });
    }
}
