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
                case "JOIN_LOBBY":
                    await connectionManager.SendMessageAsync(connectionId, "Welcome to the lobby!");
                    break;

                case "PLAY_CARD":
                    Console.WriteLine($"Player {message.PlayerId} played a card: {message.Data}");
                    break;

                case "END_TURN":
                    Console.WriteLine("End turn");
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