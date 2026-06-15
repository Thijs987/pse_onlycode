/*
    BotService drives automated (bot) players' turns.
    It reuses MatchManager for game-state mutations and ConnectionManager
    to notify the lobby, mirroring the message formats used by MessageRouter.
*/
using System.Collections.Concurrent;
using System.Text.Json;

public class BotService
{
    // Bots are identified by this prefix so the rest of the system can tell
    // them apart from human players (e.g. to decide when to drive a turn).
    public const string BotIdPrefix = "BOT_";

    private static readonly Random _random = new();

    // Tracks which bots belong to which lobby (botId -> lobbyId).
    private readonly ConcurrentDictionary<string, string> _bots = new();

    // Cards that need a 2-card "blank" combo plus a Target to resolve.
    private static readonly Dictionary<string, List<string>> ComboCardOptions = new()
    {
        { "goto", new List<string> { "goto", "vibe", "inf", "nocom" } },
        { "vibe", new List<string> { "goto", "vibe" } },
        { "inf", new List<string> { "goto", "inf" } },
        { "nocom", new List<string> { "goto", "nocom" } },
    };

    // Cards that only need a Target.
    private static readonly HashSet<string> TargetCardIds = new() { "sql" };

    private readonly MatchManager _matchManager;
    private readonly ConnectionManager _connectionManager;

    public BotService(MatchManager matchManager, ConnectionManager connectionManager)
    {
        _matchManager = matchManager;
        _connectionManager = connectionManager;
    }

    // Registers a new bot in the lobby. Because StartNewMatch builds its
    // player list from ConnectionManager.GetPlayers(lobbyId), the bot MUST be
    // added to the ConnectionManager's lobby tracking here, otherwise the
    // MatchManager will never deal it a hand or treat it as a valid player.
    // Returns the generated bot id.
    public async Task<string> AddBotAsync(string lobbyId)
    {
        string botId = BotIdPrefix + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();

        // Add to the lobby just like a human connection (minus the WebSocket).
        _connectionManager.AddToLobby(botId, lobbyId);
        _bots.TryAdd(botId, lobbyId);

        Console.WriteLine($"Bot {botId} added to lobby {lobbyId}");

        // Mirror the PLAYER_JOINED broadcast so real clients render the bot.
        var joinMessage = new NetworkMessage
        {
            Action = "PLAYER_JOINED",
            PlayerId = botId,
            Data = new DataInfo { Message = $"{botId} has joined the game!" }
        };
        await _connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(joinMessage));

        return botId;
    }

    public void RemoveBot(string botId)
    {
        _bots.TryRemove(botId, out _);
    }

    // True when the given player id belongs to a bot. Use this when a turn
    // advances to decide whether to call ProcessBotTurnAsync.
    public bool IsBot(string playerId) => _bots.ContainsKey(playerId);

    // All bot ids currently registered in the given lobby.
    public List<string> GetBots(string lobbyId) =>
        _bots.Where(b => b.Value == lobbyId).Select(b => b.Key).ToList();

    public async Task ProcessBotTurnAsync(string lobbyId, string botId)
    {
        await Task.Delay(1500);

        var pendingAction = _matchManager.GetPendingAction(lobbyId, botId);
        if (!string.IsNullOrEmpty(pendingAction))
        {
            if (pendingAction == "os")
            {
                var target = _random.Next(2) == 0 ? "take" : "top";
                var responseData = _matchManager.TryPlayCard(lobbyId, botId, new DataInfo { CardId = "os", Target = target });
                if (responseData.Error == "")
                {
                    await SendCardPlayed(lobbyId, botId, responseData);
                }
            }
            return;
        }

        bool playedCard = true;
        while (playedCard)
        {
            playedCard = false;
            var hand = _matchManager.GetPlayerHand(lobbyId, botId);

            if (hand.Contains("imp"))
            {
                var cardToDiscard = hand.FirstOrDefault(c => c != "imp");
                var cardData = new DataInfo { CardId = "imp" };
                if (cardToDiscard != null)
                {
                    cardData.Cards = new List<string> { cardToDiscard };
                }

                var responseData = _matchManager.TryPlayCard(lobbyId, botId, cardData);
                if (responseData.Error == "")
                {
                    await SendCardPlayed(lobbyId, botId, responseData);
                    
                    if (_matchManager.GetCurrentTurnPlayer(lobbyId) != botId || _matchManager.GetPendingAction(lobbyId, botId) != "")
                        return;

                    playedCard = true;
                    await Task.Delay(1500);
                    continue;
                }
            }

            foreach (var cardId in hand.Distinct().ToList())
            {
                if (cardId == "imp") continue;

                var cardData = BuildCardData(lobbyId, botId, cardId, hand);
                if (cardData == null) continue;

                var responseData = _matchManager.TryPlayCard(lobbyId, botId, cardData);
                if (responseData.Error == "")
                {
                    await SendCardPlayed(lobbyId, botId, responseData);

                    if (_matchManager.GetCurrentTurnPlayer(lobbyId) != botId || _matchManager.GetPendingAction(lobbyId, botId) != "")
                        return;

                    playedCard = true;
                    await Task.Delay(1500);
                    break;
                }
            }
        }

        await DrawCard(lobbyId, botId);
    }

    // Builds the DataInfo needed to attempt playing cardId, or null if the bot
    // can't satisfy that card's requirements (target/combo) right now.
    private DataInfo? BuildCardData(string lobbyId, string botId, string cardId, List<string> hand)
    {
        var cardData = new DataInfo { CardId = cardId };

        if (ComboCardOptions.TryGetValue(cardId, out var allowed))
        {
            var comboCards = hand.Where(allowed.Contains).Take(2).ToList();
            if (comboCards.Count < 2)
            {
                return null;
            }
            cardData.Cards = comboCards;
        }

        if (cardId == "trojan")
        {
            var sendCard = hand.FirstOrDefault(c => c != "trojan");
            if (sendCard == null)
            {
                return null;
            }
            cardData.Cards = new List<string> { sendCard };
        }

        if (cardId == "os")
        {
            cardData.Target = "view";
        }
        else if (TargetCardIds.Contains(cardId) || cardId == "trojan" || ComboCardOptions.ContainsKey(cardId))
        {
            var target = GetRandomOpponent(lobbyId, botId);
            if (target == null)
            {
                return null;
            }
            cardData.Target = target;
        }

        return cardData;
    }

    private string? GetRandomOpponent(string lobbyId, string botId)
    {
        List<string> players;
        try
        {
            players = _connectionManager.GetPlayers(lobbyId);
        }
        catch
        {
            return null;
        }

        var opponents = players.Where(p => p != botId).ToList();
        if (opponents.Count == 0)
        {
            return null;
        }

        return opponents[_random.Next(opponents.Count)];
    }

    private async Task SendCardPlayed(string lobbyId, string botId, DataInfo responseData)
    {
        var response = MakeMessage("CARD_PLAYED", botId, responseData);

        if (responseData.CardId == "imp")
        {
            await NextPlayer(lobbyId, botId, "0");
        }
        else if (responseData.IsPrivate == true)
        {
            await _connectionManager.SendMessageAsync(botId, JsonSerializer.Serialize(response));
        }
        else
        {
            await _connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(response));
        }
    }

    private async Task DrawCard(string lobbyId, string botId)
    {
        var responseData = _matchManager.GetFirstCard(lobbyId, botId);

        if (responseData.Error != "")
        {
            var errorMessage = MakeMessage("ERROR", botId, responseData);
            await _connectionManager.SendMessageAsync(botId, JsonSerializer.Serialize(errorMessage));
            return;
        }

        var publicData = new DataInfo { Target = botId };
        var response = MakeMessage("CARD_DRAWN", botId, publicData);
        await _connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(response));

        if (responseData.CardId != "imp")
        {
            await NextPlayer(lobbyId, botId, responseData.Message);
        }
    }

    private async Task NextPlayer(string lobbyId, string botId, string newLimit)
    {
        var responseData = _matchManager.NextTurn(lobbyId, botId);

        var end = _matchManager.CheckCardLimit(lobbyId, botId, newLimit);
        if (end.Error != "")
        {
            var errorMessage = MakeMessage("ERROR", botId, end);
            await _connectionManager.SendMessageAsync(botId, JsonSerializer.Serialize(errorMessage));
        }

        if (end.Message == "Removed")
        {
            var endPlayerMessage = MakeMessage("CARD_LIMIT", botId, end);
            await _connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(endPlayerMessage));
        }

        var response = MakeMessage("NEXT_TURN", botId, responseData);
        await _connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(response));
    }

    private static NetworkMessage MakeMessage(string action, string playerId, DataInfo data)
    {
        return new NetworkMessage
        {
            Action = action,
            PlayerId = playerId,
            Data = data
        };
    }
}
