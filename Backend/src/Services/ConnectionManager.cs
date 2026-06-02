using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

public class ConnectionManager
{
    // dictionary to keep track of connections
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

    public async Task HandleConnectionAsync(string connectionId, WebSocket socket, MessageRouter router)
    {
        _sockets.TryAdd(connectionId, socket);
        Console.WriteLine($"Socket Connected: {connectionId}. Total connections: {_sockets.Count}");

        var buffer = new byte[1024 * 4];

        try
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            // Keep the connection open and listen for messages
            while (!result.CloseStatus.HasValue)
            {
                string rawMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // Hand the raw JSON text over to the router to figure out what to do with it
                await router.RouteMessageAsync(connectionId, rawMessage, this);

                // Wait for the next message
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error on connection {connectionId}: {ex.Message}");
        }
        finally
        {
            _sockets.TryRemove(connectionId, out _);
            Console.WriteLine($"Socket Disconnected: {connectionId}");
        }
    }
}