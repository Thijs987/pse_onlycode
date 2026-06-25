/*
    Call MatchManager and ConnectionManager functions based on te incomming message.
*/
using System.Text.Json;
using Serilog;
using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.VisualBasic;

public class MessageRouter
{
    public readonly BotService botService;

    public MessageRouter(BotService bServ)
    {
        botService = bServ;
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

            Log.Debug("Received {Action} from {PlayerId} in {LobbyId}", message.Action, playerId, lobbyId);

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
                    string botId = await botService.AddBotAsync(lobbyId);
                    Log.Information("Bot {BotId} requested by {PlayerId} for lobby {LobbyId}", botId, playerId, lobbyId);
                    break;

                case "KICK_PLAYER":
                    var target = message.Data?.Target;
                    // Prevent kicking yourself and ensure you are the host
                    if (!string.IsNullOrEmpty(target) && target != playerId && connectionManager.IsHost(lobbyId, playerId))
                    {
                        if (botService.IsBot(target, lobbyId))
                        {
                            botService.RemoveBot(lobbyId, target);
                        }
                        await connectionManager.KickPlayerAsync(target, lobbyId, matchManager);
                    }
                    else if (target == playerId)
                    {
                        Log.Warning("{PlayerId} tried to kick themselves from {LobbyId}", playerId, lobbyId);
                    }
                    else if (!connectionManager.IsHost(lobbyId, playerId))
                    {
                        Log.Warning("{PlayerId} tried to kick {Target} from {LobbyId} but is not the host", playerId, target, lobbyId);
                    }
                    break;

                case "START_MATCH":
                    if (!connectionManager.IsHost(lobbyId, playerId))
                    {
                        Log.Warning("{PlayerId} tried to start match in {LobbyId} but is not the host", playerId, lobbyId);
                        break;
                    }

                    // matchManager.StartNewMatch(message.Data, new List<string> { message.PlayerId });
                    // Changed matchId to lobbyId
                    var players = connectionManager.GetPlayers(lobbyId);

                    GameState newState = matchManager.StartNewMatch(lobbyId, players, data);
                    if (newState.Deck.Count == 0)
                    {
                        responseData = new DataInfo { Message = "incorrect card configuration" };
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                    }
                    else
                    {
                        responseData = new DataInfo
                        {
                            NextPlayer = newState.CurrentTurnPlayerId,
                            DeckSize = newState.Deck.Count,
                            CardLimit = newState.CardLimit,
                            Players = newState.PlayerIds,
                            HandSizes = matchManager.GetPlayerHandSizes(lobbyId)
                        };

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


                        List<string> specialCards = new List<string> { "goto", "vibe", "inf", "nocom" };

                        var action = "CARD_PLAYED";

                        response = MakeMessage(action, playerId, responseData);
                        if (responseData.CardId == "imp" && !matchManager.GetPlayerHand(lobbyId, playerId).Contains("imp"))
                        {
                            await Next_player(lobbyId, playerId, false, connectionManager, matchManager);
                        }
                        else if (responseData.CardId == "imp")
                        {
                            break;
                        }
                        else if (responseData.IsPrivate == true)
                        {
                            await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                        }
                        else if (responseData.CardId == "os" && !string.IsNullOrEmpty(responseData.Target) && !responseData.Cards.Contains("imp"))
                        {
                            if (!responseData.Cards.Contains("imp"))
                            {
                                await Next_player(lobbyId, playerId, false, connectionManager, matchManager);
                            }
                            else
                            {
                                break;
                            }
                            responseData.Cards = [];
                            response = MakeMessage(action, playerId, responseData);
                        }

                        if (specialCards.Contains(responseData.CardId))
                        {
                            var dataBroad = new DataInfo
                            {
                                CardId = responseData.CardId,
                                Target = responseData.Target,
                                Cards = [responseData.Cards[0], responseData.Cards[1]]
                            };
                            var broadMessage = MakeMessage(action, playerId, dataBroad);
                            await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(broadMessage), responseData.Target);
                            await connectionManager.SendMessageAsync(responseData.Target, SerializeMsg(response));
                        }
                        else if (responseData.CardId == "trojan")
                        {
                            var dataBroad = new DataInfo
                            {
                                CardId = responseData.CardId,
                                Target = responseData.Target
                            };
                            var broadMessage = MakeMessage(action, playerId, dataBroad);
                            await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(broadMessage), responseData.Target);
                            await connectionManager.SendMessageAsync(responseData.Target, SerializeMsg(response));
                        }
                        else if (responseData.IsPrivate == false)
                        {
                            await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(response));
                        }

                        // goto/vibe/inf/nocom draw a card from the deck for the target without
                        // sending a CARD_DRAWN, so the clients' deck counter would not update.
                        // Re-sync it here, and drop the card limit if the draw exhausted and
                        // refilled the pile.
                        if (specialCards.Contains(responseData.CardId))
                        {
                            if (responseData.DeckRefilled == true)
                            {
                                matchManager.LowerCardLimit(lobbyId);
                            }
                            await BroadcastDeckSize(lobbyId, playerId, connectionManager, matchManager);
                        }

                        // For endTurncards, check if bot. These cards end the player's turn from
                        // within their own effect (rather than via DRAW_CARD), so the next player
                        // must be driven here. "os" only advances the turn on its take/top resolve
                        // phase; on the "view" phase NextPlayer is null and CheckBotTurn is a no-op.
                        if (new List<string> { "cm", "ddos", "sql", "os" }.Contains(responseData.CardId))
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
                        responseData = new DataInfo { Error = "Hand contains imp, cannot draw!" };
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                        break;
                    }

                    // Normal end to a turn. Grab card (check IH) and broadcast action to lobby.
                    responseData = matchManager.GetFirstCard(lobbyId, playerId);
                    var card = responseData.CardId;
                    var deckRefilled = responseData.DeckRefilled == true;

                    if (string.IsNullOrEmpty(card))
                    {
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                        break;
                    }

                    Log.Debug("Top card {Card} drawn for {PlayerId} in {LobbyId}", card, playerId, lobbyId);

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

                    // Check for Improved Hardware. Drawing it does not end the turn, but if this
                    // draw also exhausted and refilled the pile we must still lower the card limit
                    // and report the new deck size to all clients.
                    if (card == "imp")
                    {
                        if (deckRefilled)
                        {
                            matchManager.LowerCardLimit(lobbyId);
                            await BroadcastDeckSize(lobbyId, playerId, connectionManager, matchManager);
                        }
                        break;
                    }
                    // Switch to next player
                    await Next_player(lobbyId, playerId, deckRefilled, connectionManager, matchManager);
                    break;
            }
        }
        catch (JsonException)
        {
            Log.Warning("Received invalid JSON from {PlayerId} in {LobbyId}", playerId, lobbyId);
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

    // Broadcasts the current draw-pile size and card limit to every player in the lobby.
    public async Task BroadcastDeckSize(string lobbyId, string playerId, ConnectionManager connectionManager, MatchManager matchManager)
    {
        var deckSize = matchManager.GetDeckSize(lobbyId);
        var message = new DataInfo
        {
            Message = $"{deckSize}",
            CardLimit = matchManager.GetCardLimit(lobbyId)
        };
        var deckSizeMessage = MakeMessage("DECK_SIZE", playerId, message);
        await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(deckSizeMessage));
    }

    public async Task Next_player(string lobbyId, string playerId, bool deckRefilled, ConnectionManager connectionManager, MatchManager matchManager)
    {
        // check player card limit and remove player if over the limit
        var end = matchManager.CheckCardLimit(lobbyId, playerId, deckRefilled);

        // next turn
        var responseData = matchManager.NextTurn(lobbyId, playerId);

        // Send error if there is an error
        if (!string.IsNullOrEmpty(end.Error))
        {
            var errorMessage = MakeMessage("ERROR", playerId, end);
            await connectionManager.SendMessageAsync(playerId, SerializeMsg(errorMessage));
        }

        if (deckRefilled)
        {
            await BroadcastDeckSize(lobbyId, playerId, connectionManager, matchManager);
        }

        if (end.Message == "Removed")
        {
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
                connectionManager.RemoveLobby(playerId);
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
        if (!connectionManager.cont(lobbyId))
        {
            Log.Information("player won or sum");
            return false;
        }
        bool hasHuman = connectionManager.GetPlayers(lobbyId).Any(p => !botService.IsBot(p, lobbyId));
        if (!hasHuman)
        {
            Log.Debug("Only bots in lobby {LobbyId}", lobbyId);
            return false;
        }
        return true;
    }

    public async Task CheckBotTurn(string lobbyId, ConnectionManager connectionManager, MatchManager matchManager, DataInfo responseData)
    {
        Log.Debug("IsBot check for {PlayerId} in {LobbyId}: {IsBot}", responseData.NextPlayer, lobbyId, botService.IsBot(responseData.NextPlayer, lobbyId));
        // If the turn now lands on a bot.
        if (botService.IsBot(responseData.NextPlayer, lobbyId))
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
        while (lobbyHasHuman(lobbyId, connectionManager, matchManager) && !string.IsNullOrEmpty(current) && botService.IsBot(current, lobbyId))
        {
            await Task.Delay(500);
            Log.Debug("Pending action for {PlayerId} in {LobbyId}: {Pending}", current, lobbyId, matchManager.GetPendingAction(lobbyId, current));
            await botService.BotPlayCard(lobbyId, current);
            Log.Debug("Current turn player for {LobbyId}: {Current}", lobbyId, matchManager.GetCurrentTurnPlayer(lobbyId));
            // If the current turn player did not change and there is not a pending action.
            if (current == matchManager.GetCurrentTurnPlayer(lobbyId) && string.IsNullOrEmpty(matchManager.GetPendingAction(lobbyId, current)))
            {
                await botService.DrawCard(lobbyId, current);
            }
            current = matchManager.GetCurrentTurnPlayer(lobbyId);
        }

        // TODO: remove lobby of bots
    }
}