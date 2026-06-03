/*
    Call MatchManager and ConnectionManager functions based on te incomming message.
*/
using System.Text.Json;
using Domain;

public class MessageRouter
{
    public async Task RouteMessageAsync(string playerId, string lobbyId, string rawJson, ConnectionManager connectionManager, MatchManager matchManager)
    {
        try
        {
            var message = JsonSerializer.Deserialize<NetworkMessage>(rawJson);
            if (message == null) return;

            bool success = false;

            Console.WriteLine($"Received {message.Action} from {playerId}");

            // TODO: add actual logic
            switch (message.Action)
            {
                case "START_GAME":
                    matchManager.StartNewMatch(message.Data, new List<string> { message.PlayerId });
                    await connectionManager.SendMessageAsync(playerId, "Game Started!");
                    break;

                case "PLAY_CARD":
                    // Let's assume message.Data contains the MatchId and CardId
                    // You'd parse that JSON here, but for simplicity:
                    success = matchManager.TryPlayCard(lobbyId, message.PlayerId, message.Data);

                    if (success)
                    {
                        // If the move was valid, broadcast the result to EVERYONE in the game
                        // (You will need to add a Broadcast method to your ConnectionManager)
                        Console.WriteLine("Card played successfully.");
                    }
                    else
                    {
                        await connectionManager.SendMessageAsync(playerId, "ILLEGAL_MOVE");
                    }
                    break;

                case "END_TURN":
                    // Normal end to a turn. Grab card (check IH) and broadcast action to lobby.
                    success = matchManager.GetFirstCard(lobbyId, message.PlayerId);
                    // Get the connection Id of all player to send this to.
                    // Get player that gets card
                    if (success)
                    {
                        // If the move was valid, broadcast the result to EVERYONE in the game
                        // (You will need to add a Broadcast method to your ConnectionManager)
                        Console.WriteLine("First Card Gotten successfully.");
                    }
                    else
                    {
                        await connectionManager.SendMessageAsync(playerId, "First Card Error");
                    }

                    // connectionManager.BroadcastToLobbyAsync(lobbyId)
                    break;
            }
        }
        catch (JsonException)
        {
            Console.WriteLine("Received invalid JSON from client.");
        }
    }
}