/*
    Connection with lobbies and message sending within.
    ConnectionId == playerId?
*/
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Domain;

public class ConnectionManager
{
    private const int MaxPlayersPerLobby = 4;

    // Tracks all active WebSockets (ConnectionId -> WebSocket)
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

    // Tracks which connections are in which lobby (LobbyId -> Dictionary of ConnectionIds)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _lobbies = new();

    // Tracks which lobby a connection is currently in (ConnectionId -> LobbyId) for cleanup
    private readonly ConcurrentDictionary<string, string> _connectionToLobby = new();

    // Tracks the host of each lobby (LobbyId -> HostId)
    private readonly ConcurrentDictionary<string, string> _lobbyHosts = new();

    public async Task HandleConnectionAsync(string playerId, string lobbyId, WebSocket socket, MessageRouter router, MatchManager matchManager, CancellationToken cancellationToken)
    {
        if (_sockets.TryGetValue(playerId, out var oldSocket))
        {
            try { await oldSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnected", CancellationToken.None); } catch { }
            _sockets.TryRemove(playerId, out _);
            RemoveFromLobby(playerId, matchManager);
        }
        _sockets.TryAdd(playerId, socket);
        List<string> existingPlayers = new List<string>();
        try
        {
            existingPlayers = GetPlayers(lobbyId);
        }
        catch { }

        var rejoin = false;
        if (existingPlayers.Contains(playerId))
        {
            rejoin = true;
        }

        var joinMessage = new NetworkMessage { };

        string action;
        string message;

        if (rejoin == true)
        {
            matchManager.Rejoin(playerId);
            var responseData = new DataInfo
            {
                Cards = matchManager.GetPlayerHand(lobbyId, playerId)
            };
            var response = router.MakeMessage("HAND", playerId, responseData);
            await SendMessageAsync(playerId, System.Text.Json.JsonSerializer.Serialize(response));
            Console.WriteLine($"Socket Connected: {playerId} rejoined Lobby {lobbyId}");
            action = "PLAYER_REJOINED";
            message = $"{playerId} has rejoined the game!";
            router.botService.RemoveBot(lobbyId, playerId);
        }
        else
        {
            AddToLobby(playerId, lobbyId);
            Console.WriteLine($"Socket Connected: {playerId} joined Lobby {lobbyId}");
            action = "PLAYER_JOINED";
            message = $"{playerId} has joined the game!";
        }

        joinMessage = new NetworkMessage
        {
            Action = action,
            PlayerId = playerId,
            Data = new DataInfo
            {
                Message = message
            }
        };
        await BroadcastToLobbyAsync(lobbyId, System.Text.Json.JsonSerializer.Serialize(joinMessage));

        foreach (var existingPlayer in existingPlayers)
        {
            if (existingPlayer != playerId)
            {
                var existingPlayerMessage = new NetworkMessage
                {
                    Action = "PLAYER_JOINED",
                    PlayerId = existingPlayer,
                    Data = new DataInfo
                    {
                        Message = $"{existingPlayer} is in the game!"
                    }
                };
                await SendMessageAsync(playerId, System.Text.Json.JsonSerializer.Serialize(existingPlayerMessage));
            }
        }

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
            var responseData = matchManager.Disconnect(playerId);
            if (!matchManager.IsMatchActive(lobbyId))
            {
                RemoveFromLobby(playerId, matchManager);
            }
            else if (matchManager.GetActives(lobbyId).Count <= 0)
            {
                RemoveLobby(playerId, matchManager);
                router.botService.CleanUpLobby(lobbyId);
            }
            responseData.Message = $"{playerId} disconnected.";
            _sockets.TryRemove(playerId, out _);

            var leaveMessage = new NetworkMessage
            {
                Action = "PLAYER_LEFT",
                PlayerId = playerId,
                Data = responseData
            };

            await BroadcastToLobbyAsync(lobbyId, System.Text.Json.JsonSerializer.Serialize(leaveMessage));
            Console.WriteLine($"Socket Disconnected: {playerId}");

            if (matchManager.IsMatchActive(lobbyId) && matchManager.GetActives(lobbyId).Count > 0)
            {
                // Replace with bot
                Console.WriteLine("Adding bot");
                await router.botService.AddBotAsync(lobbyId, playerId);
                if (matchManager.GetCurrentTurnPlayer(lobbyId) == playerId)
                    await router.botService.DrawCard(lobbyId, playerId);
                // router.CheckBotTurn(lobbyId, matchManager, )
            }
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

    public void RemoveFromLobby(string connectionId, MatchManager matchManager)
    {
        if (_connectionToLobby.TryRemove(connectionId, out string? lobbyId))
        {
            if (_lobbies.TryGetValue(lobbyId, out var lobbyConnections))
            {
                lobbyConnections.TryRemove(connectionId, out _);
                if (lobbyConnections.IsEmpty)
                {
                    _lobbies.TryRemove(lobbyId, out _);
                    _lobbyHosts.TryRemove(lobbyId, out _);
                    matchManager.EndMatch(lobbyId);
                    Console.WriteLine($"Lobby {lobbyId} is empty and was destroyed.");
                }
            }
        }
    }

    public async Task KickPlayerAsync(string connectionId, string lobbyId, MatchManager matchManager)
    {
        if (_sockets.TryGetValue(connectionId, out var socket))
        {
            // Human player
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Kicked from lobby", CancellationToken.None);
            }
        }
        else
        {
            // Bot player
            RemoveFromLobby(connectionId, matchManager);

            var leaveMessage = new NetworkMessage
            {
                Action = "PLAYER_LEFT",
                PlayerId = connectionId,
                Data = new DataInfo { Message = $"{connectionId} was kicked." }
            };
            await BroadcastToLobbyAsync(lobbyId, System.Text.Json.JsonSerializer.Serialize(leaveMessage));
        }
    }

    public void RemoveLobby(string connectionId, MatchManager matchManager)
    {
        if (!_connectionToLobby.TryRemove(connectionId, out string? lobbyId))
        {
            return;
        }
        if (string.IsNullOrEmpty(lobbyId))
        {
            return;
        }
        if (_lobbies.TryRemove(lobbyId, out var lobbyConnections))
        {
            foreach (var player in lobbyConnections.Keys)
            {
                _connectionToLobby.TryRemove(player, out _);
            }
            _lobbyHosts.TryRemove(lobbyId, out _);
            matchManager.EndMatch(lobbyId);
            Console.WriteLine($"Lobby {lobbyId} is empty and was destroyed.");
        }
    }

    public async Task BroadcastToLobbyAsync(string lobbyId, string message, string exception = "")
    {
        if (_lobbies.TryGetValue(lobbyId, out var lobbyConnections))
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(bytes);

            foreach (var connectionId in lobbyConnections.Keys)
            {
                if (_sockets.TryGetValue(connectionId, out var socket) && socket.State == WebSocketState.Open
                && connectionId != exception)
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
    public string CreateLobby(string hostId)
    {
        string newLobbyId = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

        _lobbies.TryAdd(newLobbyId, new ConcurrentDictionary<string, bool>());
        if (!string.IsNullOrEmpty(hostId))
            _lobbyHosts.TryAdd(newLobbyId, hostId);

        Console.WriteLine($"Lobby {newLobbyId} created by {hostId} via HTTP.");

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

    public IEnumerable<object> GetActiveLobbies(MatchManager matchManager)
    {
        return _lobbies
            .Where(lobby => !matchManager.HasMatchStarted(lobby.Key))
            .Select(lobby => new
            {
                LobbyId = lobby.Key,
                PlayerCount = lobby.Value.Count,
                Capacity = MaxPlayersPerLobby
            });
    }
}
