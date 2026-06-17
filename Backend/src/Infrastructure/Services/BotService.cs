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

    private const int Speed = 500;

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
    private static readonly HashSet<string> TargetCardIds = ["sql", "trojan"];

    // Cards that are not playable
    private static readonly HashSet<string> UnplayableCardIds = ["blue", "err", "merge"];
    // Cards that end the turn
    private static readonly HashSet<string> EndTurnCardIds = ["cm", "ddos", "sql"];

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

    public async Task PlayerToBot(string playerId, string lobbyId)
    {
        if (_bots.TryAdd(playerId, lobbyId))
        {
            Console.WriteLine($"Player {playerId} replaced with bot");
        }
        else
        {
            Console.WriteLine($"Could not turn {playerId} to bot.");
        }
    }

    public bool RemoveBot(string botId) => _bots.TryRemove(botId, out _);

    // True when the given player id belongs to a bot.
    public bool IsBot(string playerId) => _bots.ContainsKey(playerId);

    // All bot ids currently registered in the given lobby.
    public List<string> GetBots(string lobbyId) =>
        _bots.Where(b => b.Value == lobbyId).Select(b => b.Key).ToList();

    // Let the bot play the cards in its hand
    public async Task BotPlayCard(string lobbyId, string botId)
    {
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

        var hand = _matchManager.GetPlayerHand(lobbyId, botId);
        // For imp:
        // No cards => CardId="imp", Cards=[]
        // Discard => CardId=DiscardCard, Cards=["imp", DiscardCard]
        if (hand.Contains("imp"))
        {
            var cardToDiscard = hand.FirstOrDefault(c => c != "imp", null);
            Console.WriteLine("Bot hand:");
            foreach (var card in hand)
            {
                Console.WriteLine(card);
            }
            Console.WriteLine($"Bot removes {cardToDiscard} with imp");
            var cardData = new DataInfo { CardId = "imp" };
            if (cardToDiscard != null)
            {
                cardData.CardId = cardToDiscard;
                cardData.Cards = new List<string> { "imp", cardToDiscard };
            }
            
            Console.WriteLine($"BPC cardid: {cardData.CardId}, cards:");
            foreach (var card in cardData.Cards)
            {
                Console.WriteLine(card);
            }
            // Send the resulting play to the matchmanager
            var responseData = _matchManager.TryPlayCard(lobbyId, botId, cardData);
            if (string.IsNullOrEmpty(responseData.Error))
            {
                await SendCardPlayed(lobbyId, botId, responseData);

                if (_matchManager.GetCurrentTurnPlayer(lobbyId) != botId || _matchManager.GetPendingAction(lobbyId, botId) != "")
                    return;
            }
        }
        else
        {
            // Everytime the bot plays a card, the playable cards have to be reset.
            bool playedCard = true;
            while (playedCard)
            {
                playedCard = false;
                var playableCards = PlayableCards(hand);

                foreach (var cardId in playableCards)
                {
                    await Task.Delay(Speed);
                    // Prevent it from playing the unplayable (green) cards.
                    // There should not be an imp in the bots hand
                    if (cardId == "imp")
                    {
                        Console.WriteLine($"The bot has an imp in its hand.");
                        return;
                    }

                    var cardData = BuildCardData(lobbyId, botId, cardId, hand);
                    if (cardData == null)
                    {
                        Console.WriteLine($"The bot has cardData = null. cardId: {cardId}, hand: {hand}");
                        continue;
                    }

                    // TODO: An invalid combo card match will return null.
                    // This should be prevented.
                    var responseData = _matchManager.TryPlayCard(lobbyId, botId, cardData);
                    if (string.IsNullOrEmpty(responseData.Error))
                    {
                        await SendCardPlayed(lobbyId, botId, responseData);

                        // The bot played a card.
                        // Update hand and set playedcard to true to update PlayableCards.
                        hand = _matchManager.GetPlayerHand(lobbyId, botId);
                        playedCard = true;
                        break;
                    }
                }

                // Go back to the loop in MessageRouter.
                // That will give the turn back to this bot for PendingAction handling or give turn to next one.
                if (_matchManager.GetCurrentTurnPlayer(lobbyId) != botId || !string.IsNullOrEmpty(_matchManager.GetPendingAction(lobbyId, botId)))
                    break;

            }
        }
    }

    private List<string> PlayableCards(List<string> hand)
    {
        // Remove unplayable cards
        var playableCards = hand.Where(c => !UnplayableCardIds.Contains(c)).ToList();

        // Remove unusable combo cards
        playableCards = playableCards.Where(c =>
        {
            // Check if the combo cards can be paired.
            if (ComboCardOptions.Keys.Contains(c) && ComboCardOptions.TryGetValue(c, out var allowed))
            {
                // Only get the legal cards and check if this results in more than 2
                var possiblePairs = playableCards.Where(c => allowed.Contains(c));
                if (possiblePairs.Count() >= 2)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                // Check for lonely trojan
                if (c == "trojan" && hand.Count() <= 1)
                {
                    return false;
                }

                return true;
            }
        }
        ).ToList();

        return playableCards;
    }

    // public async Task ProcessBotTurnAsync(string lobbyId, string botId)
    // {
    //     await Task.Delay(1500);

    //     var pendingAction = _matchManager.GetPendingAction(lobbyId, botId);
    //     if (!string.IsNullOrEmpty(pendingAction))
    //     {
    //         if (pendingAction == "os")
    //         {
    //             var target = _random.Next(2) == 0 ? "take" : "top";
    //             var responseData = _matchManager.TryPlayCard(lobbyId, botId, new DataInfo { CardId = "os", Target = target });
    //             if (responseData.Error == "")
    //             {
    //                 await SendCardPlayed(lobbyId, botId, responseData);
    //             }
    //         }
    //         return;
    //     }

    //     var hand = _matchManager.GetPlayerHand(lobbyId, botId);

    //     bool playedCard = true;
    //     while (playedCard)
    //     {
    //         playedCard = false;

    //         // For imp:
    //         // No cards => CardId="imp", Cards=[]
    //         // Discard => CardId=DiscardCard, Cards=["imp", DiscardCard]
    //         if (hand.Contains("imp"))
    //         {
    //             var cardToDiscard = hand.FirstOrDefault(c => c != "imp", "");
    //             var cardData = new DataInfo { CardId = "imp" };
    //             if (cardToDiscard != null)
    //             {
    //                 cardData.CardId = cardToDiscard;
    //                 cardData.Cards = new List<string> { "imp", cardToDiscard};
    //             }

    //             // Does the Imp go through TryPlayCard?
    //             var responseData = _matchManager.TryPlayCard(lobbyId, botId, cardData);
    //             if (responseData.Error == "")
    //             {
    //                 await SendCardPlayed(lobbyId, botId, responseData);

    //                 if (_matchManager.GetCurrentTurnPlayer(lobbyId) != botId || _matchManager.GetPendingAction(lobbyId, botId) != "")
    //                     return;

    //                 playedCard = true;
    //                 await Task.Delay(1500);
    //                 continue;
    //             }
    //         }

    //         foreach (var cardId in hand.Distinct().ToList())
    //         {
    //             if (cardId == "imp") continue;

    //             var cardData = BuildCardData(lobbyId, botId, cardId, hand);
    //             if (cardData == null) continue;

    //             var responseData = _matchManager.TryPlayCard(lobbyId, botId, cardData);
    //             if (responseData.Error == "")
    //             {
    //                 await SendCardPlayed(lobbyId, botId, responseData);

    //                 if (_matchManager.GetCurrentTurnPlayer(lobbyId) != botId || _matchManager.GetPendingAction(lobbyId, botId) != "")
    //                     return;

    //                 playedCard = true;
    //                 await Task.Delay(1500);
    //                 break;
    //             }
    //         }
    //     }

    //     await DrawCard(lobbyId, botId);
    // }

    // Builds the DataInfo needed to attempt playing cardId, or null if the bot
    // can't satisfy that card's requirements (target/combo) right now.
    private DataInfo? BuildCardData(string lobbyId, string botId, string cardId, List<string> hand)
    {
        var cardData = new DataInfo { CardId = cardId };

        // If cardId is a combocard, then get a pair and play them.
        if (ComboCardOptions.TryGetValue(cardId, out var allowed))
        {
            // TODO: The null gets procced.
            var comboCards = hand.Where(c => allowed.Contains(c)).Take(2).ToList();
            if (comboCards.Count < 2)
            {
                return null;
            }
            cardData.Cards = comboCards;
        }

        // It is not allowed to play a trojan horse card,
        // when you have no other cards.
        // The cardData.cards should only hold the discarded card
        if (cardId == "trojan")
        {
            if (hand.Count() >= 2)
            {
                string sendCard = hand.FirstOrDefault(c => c != "trojan", "trojan");
                cardData.Cards = new List<string> { sendCard };
            } else
            {
                Console.WriteLine("Bot entered trojan with less than 2 cards");
                cardData.Cards = new List<string> {};
            }
        }


        if (cardId == "os")
        {
            cardData.Target = "view";
        }
        else if (TargetCardIds.Contains(cardId) || ComboCardOptions.ContainsKey(cardId))
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
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }

        var opponents = players.Where(p => p != botId).ToList();
        // If == 0, then the game should be over
        if (opponents.Count == 0)
        {
            return null;
        }

        return opponents[_random.Next(opponents.Count)];
    }

    private async Task SendCardPlayed(string lobbyId, string botId, DataInfo responseData)
    {
        var response = MakeMessage("CARD_PLAYED", botId, responseData);
        // await _connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(response));
        Console.WriteLine($"cardid: {response.Data.CardId}, cards:");
        foreach (var card in response.Data.Cards)
        {
            Console.WriteLine(card);
        }

        if (responseData.CardId == "imp")
        {
            await NextPlayer(lobbyId, botId, "0");
        }
        
        if (responseData.IsPrivate == true)
        {
            await _connectionManager.SendMessageAsync(botId, JsonSerializer.Serialize(response));
        }
        else
        {
            await _connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(response));
        }
    }

    public async Task DrawCard(string lobbyId, string botId)
    {
        var responseData = _matchManager.GetFirstCard(lobbyId, botId);

        if (!string.IsNullOrEmpty(responseData.Error))
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

            // Check if there is a winner
            string winnerId = _matchManager.GetWinner(lobbyId);
            if (!string.IsNullOrEmpty(winnerId))
            {
                var winnerData = new DataInfo { NextPlayer = winnerId };
                var gameOverMessage = MakeMessage("GAME_OVER", winnerId, winnerData);
                await _connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(gameOverMessage));
                return; // Stop broadcasting NEXT_TURN
            }
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
