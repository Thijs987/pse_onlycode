/*
    Connection with lobbies and message sending within.
    ConnectionId == playerId?
*/
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

public class ConnectionManager
{
    private const int MaxPlayersPerLobby = 4;

    // Tracks all active WebSockets (ConnectionId -> WebSocket)
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

    // Tracks which connections are in which lobby (LobbyId -> Dictionary of ConnectionIds)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _lobbies = new();

    // Tracks which lobby a connection is currently in (ConnectionId -> LobbyId) for cleanup
    private readonly ConcurrentDictionary<string, string> _connectionToLobby = new();

    public async Task HandleConnectionAsync(string playerId, string lobbyId, WebSocket socket, MessageRouter router, MatchManager matchManager, CancellationToken cancellationToken)
    {
        _sockets.TryAdd(playerId, socket);
        AddToLobby(playerId, lobbyId);
        Console.WriteLine($"Socket Connected: {playerId} joined Lobby {lobbyId}");

        var joinMessage = new NetworkMessage
        {
            Action = "PLAYER_JOINED",
            PlayerId = playerId,
            Data = new DataInfo {
                Message = $"{playerId} has joined the game!"
            }
        };

        await BroadcastToLobbyAsync(lobbyId, System.Text.Json.JsonSerializer.Serialize(joinMessage));

        var buffer = new byte[1024 * 4];

        try
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            while (!result.CloseStatus.HasValue && !cancellationToken.IsCancellationRequested)
            {
                string rawMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await router.RouteMessageAsync(playerId, lobbyId, rawMessage, this, matchManager);
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }

            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(
                    result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                    result.CloseStatusDescription ?? "Normal closure",
                    CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // For when we shut the server down manually with ctrl+c
            Console.WriteLine($"Server shutting down, forcing disconnect for {playerId}...");

            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Server shutting down", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error on connection {playerId}: {ex.Message}");
        }
        finally
        {
            RemoveFromLobby(playerId);
            _sockets.TryRemove(playerId, out _);

            var leaveMessage = new NetworkMessage
            {
                Action = "PLAYER_LEFT",
                PlayerId = playerId,
                Data = new DataInfo {
                    Message = $"{playerId} disconnected."
                }
            };

            await BroadcastToLobbyAsync(lobbyId, System.Text.Json.JsonSerializer.Serialize(leaveMessage));
            Console.WriteLine($"Socket Disconnected: {playerId}");
        }
    }

    public List<string> GetPlayers(string lobbyId)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobbyConnections))
        {
            throw new Exception($"No lobby {lobbyId} found");
        }
        List<string> players = lobbyConnections.Keys.ToList<string>();
        return players;
    }

    public void AddToLobby(string connectionId, string lobbyId)
    {
        var lobbyConnections = _lobbies.GetOrAdd(lobbyId, _ => new ConcurrentDictionary<string, bool>());

        lobbyConnections.TryAdd(connectionId, true);

        _connectionToLobby.TryAdd(connectionId, lobbyId);
    }

    public void RemoveFromLobby(string connectionId)
    {
        if (_connectionToLobby.TryRemove(connectionId, out string? lobbyId))
        {
            if (_lobbies.TryGetValue(lobbyId, out var lobbyConnections))
            {
                lobbyConnections.TryRemove(connectionId, out _);
                if (lobbyConnections.IsEmpty)
                {
                    _lobbies.TryRemove(lobbyId, out _);
                    Console.WriteLine($"Lobby {lobbyId} is empty and was destroyed.");
                }
            }
        }
    }

    public async Task BroadcastToLobbyAsync(string lobbyId, string message)
    {
        if (_lobbies.TryGetValue(lobbyId, out var lobbyConnections))
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(bytes);

            foreach (var connectionId in lobbyConnections.Keys)
            {
                if (_sockets.TryGetValue(connectionId, out var socket) && socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }

    public async Task BroadcastToLobbyExceptAsync(string lobbyId, string message, string playerId)
    {
        if (_lobbies.TryGetValue(lobbyId, out var lobbyConnections))
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(bytes);

            foreach (var connectionId in lobbyConnections.Keys)
            {
                if (_sockets.TryGetValue(connectionId, out var socket) && socket.State == WebSocketState.Open
                && connectionId != playerId)
                {
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }


    public async Task SendMessageAsync(string connectionId, string message)
    {
        if (_sockets.TryGetValue(connectionId, out var socket) && socket.State == WebSocketState.Open)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    // HTTP things
    public string CreateLobby()
    {
        string newLobbyId = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

        _lobbies.TryAdd(newLobbyId, new ConcurrentDictionary<string, bool>());
        Console.WriteLine($"Lobby {newLobbyId} created via HTTP.");

        return newLobbyId;
    }

    public bool IsLobbyAvailable(string lobbyId)
    {
        if (_lobbies.TryGetValue(lobbyId, out var lobbyConnections))
        {
            if (lobbyConnections.Count < MaxPlayersPerLobby)
            {
                return true;
            }
        }
        return false;
    }

    // Only returns lobbies with less than 4 players like this
    public IEnumerable<object> GetActiveLobbies()
    {
        return _lobbies
            .Where(lobby => lobby.Value.Count < MaxPlayersPerLobby)
            .Select(lobby => new
            {
                LobbyId = lobby.Key,
                PlayerCount = lobby.Value.Count
            });
    }
}
