using System.Text.Json;

public class MessageRouter
{
    public async Task RouteMessageAsync(string connectionId, string rawJson, ConnectionManager connectionManager, MatchManager matchManager)
    {
        try
        {
            var message = JsonSerializer.Deserialize<NetworkMessage>(rawJson);
            if (message == null) return;

            Console.WriteLine($"Received {message.Action} from {connectionId}");

            // TODO: add actual logic
            switch (message.Action)
            {
                case "START_GAME":
                    matchManager.StartNewMatch(message.Data, new List<string> { message.PlayerId });
                    await connectionManager.SendMessageAsync(connectionId, "Game Started!");
                    break;

                case "PLAY_CARD":
                    // Let's assume message.Data contains the MatchId and CardId
                    // You'd parse that JSON here, but for simplicity:
                    bool success = matchManager.TryPlayCard("match_123", message.PlayerId, message.Data);

                    if (success)
                    {
                        // If the move was valid, broadcast the result to EVERYONE in the game
                        // (You will need to add a Broadcast method to your ConnectionManager)
                        Console.WriteLine("Card played successfully.");
                    }
                    else
                    {
                        await connectionManager.SendMessageAsync(connectionId, "ILLEGAL_MOVE");
                    }
                    break;
            }
        }
        catch (JsonException)
        {
            Console.WriteLine("Received invalid JSON from client.");
        }
    }
}