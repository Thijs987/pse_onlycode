using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain;
using Xunit;

namespace Backend.Tests;

public class MessageRouterTests
{
    [Fact]
    public async Task CheckBotTurn_ShouldNotThrow_WhenLobbyIsDestroyed()
    {
        var connectionManager = new ConnectionManager();
        var matchManager = new MatchManager(Array.Empty<ICardEffect>(), connectionManager);
        var botService = new BotService(matchManager, connectionManager);
        var router = new MessageRouter(botService);

        string lobbyId = "testLobby";
        string humanId = "human1";

        connectionManager.CreateLobby(humanId);
        connectionManager.AddToLobby(humanId, lobbyId);

        string botId = await botService.AddBotAsync(lobbyId);

        // Simulate game over / lobby destruction
        connectionManager.RemoveLobby(humanId);

        // Ensure the lobby is actually gone so that lobbyHasHuman will trigger its catch block
        Assert.Throws<Exception>(() => connectionManager.GetPlayers(lobbyId));

        var responseData = new DataInfo { NextPlayer = botId };

        // This should not throw an exception anymore because lobbyHasHuman handles the missing lobby
        var exception = await Record.ExceptionAsync(() => router.CheckBotTurn(lobbyId, connectionManager, matchManager, responseData));

        Assert.Null(exception);
    }
}
