public static class LobbyEndpoints
{
    public static void MapLobbyEndpoints(this WebApplication app)
    {
        // Group all lobby routes together under shared /api/lobbies prefix
        var group = app.MapGroup("/api/lobbies");

        group.MapGet("/active", (ConnectionManager wsManager) =>
        {
            return Results.Ok();
        });

        group.MapPost("/create", (string hostId) =>
        {
            return Results.Created();
        });
    }
}