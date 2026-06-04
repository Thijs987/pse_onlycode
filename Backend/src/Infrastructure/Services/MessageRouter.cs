/*
    Call MatchManager and ConnectionManager functions based on te incomming message.
*/
using System.Text.Json;
using Domain;
using Microsoft.AspNetCore.Identity;
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
            NetworkMessage response;

            Console.WriteLine($"Received {message.Action} from {playerId}");

            // TODO: add actual logic
            switch (message.Action)
            {
                case "START_MATCH":
                    // matchManager.StartNewMatch(message.Data, new List<string> { message.PlayerId });
                    // Changed matchId to lobbyId
                    var players = connectionManager.GetPlayers(lobbyId);

                    responseData = matchManager.StartNewMatch(lobbyId, players);

                    response = MakeMessage("MATCH_STARTED", playerId, responseData);
                    await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(response));
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

                        response = MakeMessage("CARD_PLAYED", playerId, responseData);
                        await connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(response));
                    }
                    else
                    {
                        response = MakeMessage("ERROR", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(response));
                    }
                    break;

                case "DRAW_CARD":
                    // Normal end to a turn. Grab card (check IH) and broadcast action to lobby.
                    responseData = matchManager.GetFirstCard(lobbyId, message.PlayerId);
                    var card = responseData.CardId;
                    var newLimit = responseData.Message;

                    if (card == "")
                    {
                        response = MakeMessage("Error", playerId, responseData);
                        await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(response));
                        break;
                    }

                    Console.WriteLine($"Gotten top card {card} succesfully!");

                    // Check for Improved Hardware
                    if (card == "Improved Hardware")
                    {
                        // Not handling that shit yet
                    }

                    // Send card to player.
                    response = MakeMessage("CARD_DRAWN", playerId, responseData);
                    await connectionManager.SendMessageAsync(playerId, JsonSerializer.Serialize(response));

                    // next turn
                    responseData = matchManager.NextTurn(lobbyId, playerId);

                    // check player card limit and remove player if over the limit
                    var end = matchManager.CheckCardLimit(lobbyId, playerId, newLimit);
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
                        Data = responseData
                    };
                    responseData = matchManager.NextTurn(lobbyId, playerId);

                    // Broadcast next player and NTurns
                    response = MakeMessage("NEXT_TURN", playerId, responseData);
                    await connectionManager.BroadcastToLobbyAsync(lobbyId, JsonSerializer.Serialize(response));

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
}
