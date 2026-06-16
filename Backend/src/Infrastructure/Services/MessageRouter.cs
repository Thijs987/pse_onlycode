/*
    Call MatchManager and ConnectionManager functions based on te incomming message.
*/
using System.Text.Json;
using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.VisualBasic;

public class MessageRouter
{
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
                case "START_MATCH":
                    // matchManager.StartNewMatch(message.Data, new List<string> { message.PlayerId });
                    // Changed matchId to lobbyId
                    var players = connectionManager.GetPlayers(lobbyId);

                    GameState newState = matchManager.StartNewMatch(lobbyId, players);
                    responseData = new DataInfo { NextPlayer = newState.CurrentTurnPlayerId };

                    foreach (var player in players)
                    {
                        responseData.Cards = newState.PlayerHands[player];
                        response = MakeMessage("MATCH_STARTED", player, responseData);
                        await connectionManager.SendMessageAsync(player, SerializeMsg(response));
                    }

                    // await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                    // await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(response));
                    break;

                case "PLAY_CARD":
                    // Let's assume message.Data contains the MatchId and CardId
                    // You'd parse that JSON here, but for simplicity:
                    responseData = matchManager.TryPlayCard(lobbyId, playerId, data);

                    if (string.IsNullOrEmpty(responseData.Error))
                    {
                        // If the move was valid, broadcast the result to EVERYONE in the game
                        // (You will need to add a Broadcast method to your ConnectionManager)
                        Console.WriteLine("Card played successfully.");


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
                        else if (specialCards.Contains(responseData.CardId)) {
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
                        else {
                            await connectionManager.BroadcastToLobbyAsync(lobbyId, SerializeMsg(response));
                        }
                    }
                    else
                    {
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, SerializeMsg(response));
                    }
                    break;

                case "DRAW_CARD":
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
        // next turn
        var responseData = matchManager.NextTurn(lobbyId, playerId);

        // check player card limit and remove player if over the limit
        var end = matchManager.CheckCardLimit(lobbyId, playerId, newLimit);
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
    }
}
