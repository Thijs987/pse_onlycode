using System.Text.Json;

public class MessageRouter
{
    public async Task RouteMessageAsync(string connectionId, string rawJson, ConnectionManager connectionManager)
    {
        try
        {
            var message = JsonSerializer.Deserialize<NetworkMessage>(rawJson);
            if (message == null) return;

            Console.WriteLine($"Received {message.Action} from {connectionId}");

            // TODO: add actual logic
            switch (message.Action)
            {
                // Game actions
                case "START_GAME":
                    await connectionManager.SendMessageAsync(connectionId, "Game Started!");
                    break;

                case "PLAY_CARD":
                    await connectionManager.SendMessageAsync(connectionId, "ILLEGAL_MOVE");
                    break;

                default:
                    Console.WriteLine($"Unknown action: {message.Action}");
                    break;
            }
        }
        catch (JsonException)
        {
            Console.WriteLine("Received invalid JSON from client.");
        }
    }
}