/*
    Call MatchManager and ConnectionManager functions based on te incomming message.
*/
using System.Text.Json;
using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.VisualBasic;

public class MessageRouter
{
    private readonly BotService _botService;

    public MessageRouter(BotService botService)
    {
        _botService = botService;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private string SerializeMsg(NetworkMessage msg)
    {
        return JsonSerializer.Serialize(msg, _jsonOptions);
    }

    public async Task RouteMessageAsync(string playerId, string lobbyId, string rawJson, ConnectionManager connectionManager, MatchManager matchManager)
    {
        try
        {
            var message = JsonSerializer.Deserialize<NetworkMessage>(rawJson);
            if (message == null) return;
            var data = message.Data;
            // bool success = false;

            // For response
            DataInfo responseData;
            NetworkMessage response;

            Console.WriteLine($"Received {message.Action} from {playerId}");

            // TODO: add actual logic
            switch (message.Action)
            {
                case "ADD_BOT":
                    // Can't add a bot to a match that's already running.
                    if (matchManager.IsMatchActive(lobbyId))
                    {
                        responseData = new DataInfo { Error = "Cannot add a bot after the match has started." };
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(response));
                        break;
                    }

                    // Reject if the lobby is already full.
                    if (!connectionManager.IsLobbyAvailable(lobbyId))
                    {
                        responseData = new DataInfo { Error = "Lobby is full." };
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(response));
                        break;
                    }

                    // Registers a new bot in the lobby. AddBotAsync adds it to the
                    // ConnectionManager lobby (so START_MATCH includes it) and
                    // broadcasts PLAYER_JOINED for the new bot itself.
                    string botId = await _botService.AddBotAsync(lobbyId);
                    Console.WriteLine($"Bot {botId} requested by {playerId} for lobby {lobbyId}");
                    break;

                case "KICK_PLAYER":
                    var target = message.Data?.Target;
                    if (!string.IsNullOrEmpty(target))
                    {
                        if (_botService.IsBot(target))
                        {
                            _botService.RemoveBot(target);
                        }
                        await connectionManager.KickPlayerAsync(target, lobbyId, matchManager);
                    }
                    break;

                case "START_MATCH":
                    // matchManager.StartNewMatch(message.Data, new List<string> { message.PlayerId });
                    // Changed matchId to lobbyId
                    var players = connectionManager.GetPlayers(lobbyId);

                    GameState newState = matchManager.StartNewMatch(lobbyId, players, data);
                    if(newState.Deck.Count == 0) {
                        responseData = new DataInfo {Message = "incorrect card configuration"};
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                    } else {
                        responseData = new DataInfo { NextPlayer = newState.CurrentTurnPlayerId };

                        foreach (var player in players)
                        {
                            responseData.Cards = newState.PlayerHands[player];
                            response = MakeMessage("MATCH_STARTED", player, responseData);
                            await connectionManager.SendMessageAsync(player, SerializeMsg(response));
                        }
                    }

                    // If the first player up is a bot, let it play immediately.
                    // await DriveBotsAsync(lobbyId, connectionManager, matchManager);
                    await CheckBotTurn(lobbyId, connectionManager, matchManager, responseData);
                    break;



                case "PLAY_CARD":
                    // Let's assume message.Data contains the MatchId and CardId
                    // You'd parse that JSON here, but for simplicity:
                    responseData = matchManager.TryPlayCard(lobbyId, playerId, data);

                    if (string.IsNullOrEmpty(responseData.Error))
                    {
                        // If the move was valid, broadcast the result to EVERYONE in the game


                        List <string> specialCards = new List <string> {"goto", "vibe", "inf", "nocom"};

                        var action = "CARD_PLAYED";

                        response = MakeMessage(action, playerId, responseData);
                        if (responseData.CardId == "imp") {
                            await Next_player(lobbyId, playerId, "0", connectionManager, matchManager);
                        }
                        else if (responseData.IsPrivate == true)
                        {
                            await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                        }

                        if (specialCards.Contains(responseData.CardId)) {
                            var dataBroad = new DataInfo {
                                Target = responseData.Target,
                                Cards = responseData.Cards
                            };
                            var broadMessage = MakeMessage(action,playerId, dataBroad);
                            await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(broadMessage), responseData.Target);
                            await connectionManager.SendMessageAsync(responseData.Target, SerializeMsg(response));
                        } else if (responseData.CardId == "trojan") {
                            var dataBroad = new DataInfo {
                                CardId = responseData.CardId,
                                Target = responseData.Target
                            };
                            var broadMessage = MakeMessage(action,playerId, dataBroad);
                            await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(broadMessage), responseData.Target);
                            await connectionManager.SendMessageAsync(responseData.Target, SerializeMsg(response));
                        }
                        else if (responseData.IsPrivate == false) {
                            await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(response));
                        }

                        // For endTurncards, check if bot.
                        if (new List<string> { "cm", "ddos", "sql" }.Contains(responseData.CardId))
                        {
                            await CheckBotTurn(lobbyId, connectionManager, matchManager, responseData);
                        }

                        // When is the use of this code?
                        // if (responseData.CardId != "imp" && matchManager.GetCurrentTurnPlayer(lobbyId) != playerId)
                        // {
                        //     await DriveBotsAsync(lobbyId, connectionManager, matchManager);
                        // }
                    }
                    else
                    {
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                    }
                    break;

                case "DRAW_CARD":
                    // Check for Improved Hardware
                    if (matchManager.GetPlayerHand(lobbyId, playerId).Contains("imp"))
                    {
                        responseData = new DataInfo { Error = "Hand cotnains imp, cannot draw!"};
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                        break;
                    }

                    // Normal end to a turn. Grab card (check IH) and broadcast action to lobby.
                    responseData = matchManager.GetFirstCard(lobbyId, playerId);
                    var card = responseData.CardId;
                    var newLimit = responseData.Message;

                    if (string.IsNullOrEmpty(card))
                    {
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                        break;
                    }

                    Console.WriteLine($"Gotten top card {card} succesfully!");

                    // Send card to player.
                    response = MakeMessage("CARD_DRAWN", playerId, responseData);
                    await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));

                    // Send card message to all other players
                    var Data = new DataInfo
                    {
                        Target = playerId
                    };
                    response = MakeMessage("CARD_DRAWN", playerId, Data);
                    await connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(response), playerId);

                    // Check for Improved Hardware
                    if (card == "imp")
                    {
                        break;
                    }
                    // Switch to next player
                    await Next_player(lobbyId, playerId, newLimit, connectionManager, matchManager);
                    break;
            }
        }
        catch (JsonException)
        {
            Console.WriteLine("Received invalid JSON from client.");
        }
    }

    public NetworkMessage MakeMessage(string action, string playerId, DataInfo messageData)
    {
        var message = new NetworkMessage
        {
            Action = action,
            PlayerId = playerId,
            Data = messageData
        };
        return message;
    }

    public async Task Next_player(string lobbyId, string playerId, string newLimit, ConnectionManager connectionManager, MatchManager matchManager)
    {
        // check player card limit and remove player if over the limit
        var end = matchManager.CheckCardLimit(lobbyId, playerId, newLimit);

        // next turn
        var responseData = matchManager.NextTurn(lobbyId, playerId);

        // Send error if there is an error
        if (!string.IsNullOrEmpty(end.Error))
        {
            var errorMessage = MakeMessage("ERROR", playerId, end);
            await connectionManager.SendMessageAsync(playerId, SerializeMsg(errorMessage));
        }

        if(newLimit == "1") {
            var deck_size = matchManager.GetDeckSize(lobbyId);
            var message = new DataInfo {
                Message = $"{deck_size}"
                };
            var limitMessage = MakeMessage("DECK_SIZE", playerId,message);
            //broadcast remove player
            await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(limitMessage));
        }

        if (end.Message == "Removed") {
            var endPlayerMessage = MakeMessage("CARD_LIMIT", playerId, end);
            //broadcast remove player
            await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(endPlayerMessage));

            // Check if there is a winner
            string winnerId = matchManager.GetWinner(lobbyId);
            if (!string.IsNullOrEmpty(winnerId))
            {
                var winnerData = new DataInfo { NextPlayer = winnerId };
                var gameOverMessage = MakeMessage("GAME_OVER", winnerId, winnerData);
                await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(gameOverMessage));
                return; // Stop broadcasting NEXT_TURN
            }
        }

        // Broadcast next player and NTurns
        var response = MakeMessage("NEXT_TURN", playerId, responseData);
        await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(response));

        await CheckBotTurn(lobbyId, connectionManager, matchManager, responseData);
    }

    private bool lobbyHasHuman(string lobbyId, ConnectionManager connectionManager, MatchManager matchManager)
    {
        bool hasHuman;
        try
        {
            hasHuman = connectionManager.GetPlayers(lobbyId).Any(p => !_botService.IsBot(p));
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return false;
        }
        if (!hasHuman)
        {
            Console.WriteLine($"Only bots in lobby");
            return false;
        }
        return true;
    }

    public async Task CheckBotTurn(string lobbyId, ConnectionManager connectionManager, MatchManager matchManager, DataInfo responseData)
    {
        // If the turn now lands on a bot.
        if (_botService.IsBot(responseData.NextPlayer))
        {
            await BotTurn(lobbyId, connectionManager, matchManager);
        }
    }

    // Plays out every bot whose turn it currently is, chaining through
    // consecutive bots until the turn lands on a human (or the match ends).
    // Each bot's own turn advance + broadcasts happen inside ProcessBotTurnAsync;
    // this loop only decides whether the *next* player up is another bot.
    // private async Task DriveBotsAsync(string lobbyId, ConnectionManager connectionManager, MatchManager matchManager)
    private async Task BotTurn(string lobbyId, ConnectionManager connectionManager, MatchManager matchManager)
    {
        // The bot has to wait before playing for a race condition.
        // If the bot has the first turn in the match, playing too fast will mess up the playerhand.gd _ready because of multiple messages.
        await Task.Delay(1500);
        // Don't drive bots if there's no human left to play against,
        // otherwise bots would pass the turn among themselves forever.
        var current = matchManager.GetCurrentTurnPlayer(lobbyId);
        while (lobbyHasHuman(lobbyId, connectionManager, matchManager) && !string.IsNullOrEmpty(current) && _botService.IsBot(current))
        {
            await Task.Delay(500);
            await _botService.BotPlayCard(lobbyId, current);
            // If the current turn player did not change and there is not a pending action.
            if (current == matchManager.GetCurrentTurnPlayer(lobbyId) && string.IsNullOrEmpty(matchManager.GetPendingAction(lobbyId, current)))
            {
                await _botService.DrawCard(lobbyId, current);
            }
            current = matchManager.GetCurrentTurnPlayer(lobbyId);
        }

        // TODO: remove lobby of bots
        // TODO normal turn procs drawcard and thus only plays one card.
        // But why? There is a foreach loop in botservice.botplaycard.
        // This should keep it in there and play everyuthing playable.
    }
    //     private async Task BotTurn(string lobbyId, ConnectionManager connectionManager, MatchManager matchManager)
    //     {
    //         var current = matchManager.GetCurrentTurnPlayer(lobbyId);
    //         while (_botService.hasHuman() && !string.IsNullOrEmpty(current) && _botService.IsBot(current))
    //         {
    //             await _botService.ProcessBotTurnAsync(lobbyId, current);
    //             current = matchManager.GetCurrentTurnPlayer(lobbyId);
    //         }
    //     }
}
