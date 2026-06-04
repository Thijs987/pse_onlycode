/*
    Call MatchManager and ConnectionManager functions based on te incomming message.
*/
using System.Text.Json;
using Domain;

public class MessageRouter
{
    public async Task RouteMessageAsync(string playerId, string lobbyId, string rawJson, ConnectionManager connectionManager, MatchManager matchManager)
    {
        try
        {
            var message = JsonSerializer.Deserialize<NetworkMessage>(rawJson);
            var data = message.Data;
            if (message == null) return;

            // bool success = false;

            Console.WriteLine($"Received {message.Action} from {playerId}");

            // TODO: add actual logic
            switch (message.Action)
            {
                case "START_GAME":
                    // matchManager.StartNewMatch(message.Data, new List<string> { message.PlayerId });
                    // Changed matchId to lobbyId
                    var players = connectionManager.GetPlayers(lobbyId);
                    matchManager.StartNewMatch(lobbyId, players);
                    await connectionManager.SendMessageAsync(playerId, "Game Started!");
                    break;

                case "PLAY_CARD":
                    // Let's assume message.Data contains the MatchId and CardId
                    // You'd parse that JSON here, but for simplicity:
                    var result = matchManager.TryPlayCard(lobbyId, message.PlayerId, data);

                    if (result.Error == "")
                    {
                        // If the move was valid, broadcast the result to EVERYONE in the game
                        // (You will need to add a Broadcast method to your ConnectionManager)
                        Console.WriteLine("Card played successfully.");
                        var cardPlayedMessage = new NetworkMessage
                            {
                                Action = "CARD_PLAYED",
                                PlayerId = playerId,
                                Data = result
                            };

                        await connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(cardPlayedMessage));
                    }
                    else
                    {
                        await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(result));
                    }
                    break;

                case "DRAW_CARD":
                    // Normal end to a turn. Grab card (check IH) and broadcast action to lobby.
                    string card = matchManager.GetFirstCard(lobbyId, message.PlayerId);
                    // Get the connection Id of all player to send this to.
                    // Get player that gets card
                    if (card == "")
                    {
                        await connectionManager.SendMessageAsync(playerId, "First Card Error");
                        break;
                    }

                    Console.WriteLine($"Gotten top card {card} succesfully!");

                    // Check for Improved Hardware
                    if (card == "Improved Hardware")
                    {
                        // Not handling that shit yet
                    }

                    // Send card to player.
                    var sendCardMessage = new NetworkMessage
                    {
                        Action = "SEND_CARD",
                        PlayerId = playerId,
                        Data = new DataInfo{
                            CardId = card
                        }
                    };

                    await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(sendCardMessage));
                    // next turn
                    (string nextPlayer, int nTurns) = matchManager.NextTurn(lobbyId, playerId);

                    // check player card limit and remove player if over the limit
                    var end = matchManager.CheckCardLimit(lobbyId, playerId);
                    // Send error if there is an error
                    if(end.Error != ""){
                        var errorMessage = new NetworkMessage
                        {
                            Action = "ERROR",
                            PlayerId = playerId,
                            Data = end
                        };
                        await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(errorMessage));
                    }

                    if(end.Message == "Removed") {
                        var endPlayerMessage = new NetworkMessage
                        {
                            Action = "CARD_LIMIT",
                            PlayerId = playerId
                        };
                        //broadcast remove player
                        await connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(endPlayerMessage));
                    }

                    var endTurnMessage = new NetworkMessage
                    {
                        Action = "CARD_DRAWN",
                        PlayerId = playerId,
                        Data = new DataInfo{
                            NextPlayer = nextPlayer,
                            Turns = nTurns
                        }
                    };

                    // Broadcast next player and NTurns
                    await connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(endTurnMessage));

                    break;
            }
        }
        catch (JsonException)
        {
            Console.WriteLine("Received invalid JSON from client.");
        }
    }
}
