/*
    Call MatchManager and ConnectionManager functions based on te incomming message.
*/
using System.Text.Json;
using Domain;
using Microsoft.VisualBasic;

public class MessageRouter
{
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

            Console.WriteLine($"Received {message.Action} from {playerId}");

            // TODO: add actual logic
            switch (message.Action)
            {
                case "START_MATCH":
                    // matchManager.StartNewMatch(message.Data, new List<string> { message.PlayerId });
                    // Changed matchId to lobbyId
                    var players = connectionManager.GetPlayers(lobbyId);
                    responseData = matchManager.StartNewMatch(lobbyId, players);
                    var matchStartMessage = new NetworkMessage
                    {
                        Action = "MATCH_STARTED",
                        PlayerId = playerId,
                        Data = responseData
                    };
                    await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(matchStartMessage));
                    break;

                case "PLAY_CARD":
                    // Let's assume message.Data contains the MatchId and CardId
                    // You'd parse that JSON here, but for simplicity:
                    responseData = matchManager.TryPlayCard(lobbyId, message.PlayerId, data);

                    if (responseData.Error == "")
                    {
                        // If the move was valid, broadcast the result to EVERYONE in the game
                        // (You will need to add a Broadcast method to your ConnectionManager)
                        Console.WriteLine("Card played successfully.");
                        var cardPlayedMessage = new NetworkMessage
                        {
                            Action = "CARD_PLAYED",
                            PlayerId = playerId,
                            Data = responseData
                        };

                        await connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(cardPlayedMessage));
                    }
                    else
                    {
                        await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(responseData));
                    }
                    break;

                case "DRAW_CARD":
                    // Normal end to a turn. Grab card (check IH) and broadcast action to lobby.
                    responseData = matchManager.GetFirstCard(lobbyId, message.PlayerId);
                    var card = responseData.CardId;

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
                    await connectionManager.SendMessageAsync(playerId, $"Got {card}");

                    // next turn
                    responseData = matchManager.NextTurn(lobbyId, playerId);

                    var endTurnMessage = new NetworkMessage
                    {
                        Action = "CARD_DRAWN",
                        PlayerId = playerId,
                        Data = responseData
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
