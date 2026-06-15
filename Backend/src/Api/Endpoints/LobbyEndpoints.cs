using System.Collections.Concurrent;

public static class LobbyEndpoints
{
    public static void MapLobbyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/lobbies");

        // GET /api/lobbies/active
        group.MapGet("/active", (ConnectionManager manager) =>
        {
            var activeLobbies = manager.GetActiveLobbies();
            return Results.Ok(activeLobbies);
        });

        // POST /api/lobbies/create?hostId=PLayer_x
        group.MapPost("/create", (string hostId, ConnectionManager manager) =>
        {
            string newLobbyId = manager.CreateLobby(hostId);
            return Results.Ok(new { LobbyId = newLobbyId });
        });
    }
}