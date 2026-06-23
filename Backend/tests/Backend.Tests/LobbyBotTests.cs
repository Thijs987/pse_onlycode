using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain;
using Xunit;

namespace Backend.Tests;

public class LobbyBotTests
{
    private (ConnectionManager, MatchManager, BotService) SetupServices()
    {
        var connectionManager = new ConnectionManager();
        // Pass empty list of card effects since we just test lobby state
        var matchManager = new MatchManager(Array.Empty<ICardEffect>(), connectionManager);
        var botService = new BotService(matchManager, connectionManager);

        return (connectionManager, matchManager, botService);
    }

    [Fact]
    public async Task Lobby_ShouldCleanUp_WhenBotWins_1Bot()
    {
        var (connectionManager, matchManager, botService) = SetupServices();
        string humanId = "Noah";
        string lobbyId = connectionManager.CreateLobby(humanId);
        connectionManager.AddToLobby(humanId, lobbyId);

        string bot1 = await botService.AddBotAsync(lobbyId);

        matchManager.StartNewMatch(lobbyId, connectionManager.GetPlayers(lobbyId), new DataInfo());

        Assert.Equal(2, connectionManager.GetPlayers(lobbyId).Count);
        Assert.Single(botService.GetBots(lobbyId));
        Assert.True(matchManager.IsMatchActive(lobbyId));

        // Simulate Bot winning
        connectionManager.RemoveLobby(bot1);

        Assert.Throws<Exception>(() => connectionManager.GetPlayers(lobbyId));
        Assert.Empty(botService.GetBots(lobbyId));
        Assert.False(matchManager.IsMatchActive(lobbyId));
        var activeLobbies = connectionManager.GetActiveLobbies(matchManager);
        Assert.Empty(activeLobbies);
    }

    [Fact]
    public async Task Lobby_ShouldCleanUp_WhenHumanWins_2Bots()
    {
        var (connectionManager, matchManager, botService) = SetupServices();
        string humanId = "Noah_W";
        string lobbyId = connectionManager.CreateLobby(humanId);
        connectionManager.AddToLobby(humanId, lobbyId);

        string bot1 = await botService.AddBotAsync(lobbyId);
        string bot2 = await botService.AddBotAsync(lobbyId);

        matchManager.StartNewMatch(lobbyId, connectionManager.GetPlayers(lobbyId), new DataInfo());

        Assert.Equal(3, connectionManager.GetPlayers(lobbyId).Count);
        Assert.Equal(2, botService.GetBots(lobbyId).Count);

        // Simulate Noah winning
        connectionManager.RemoveLobby(humanId);

        Assert.Throws<Exception>(() => connectionManager.GetPlayers(lobbyId));
        Assert.Empty(botService.GetBots(lobbyId));
        Assert.False(matchManager.IsMatchActive(lobbyId));
    }

    [Fact]
    public async Task Lobby_ShouldCleanUp_WhenBotWins_3Bots()
    {
        var (connectionManager, matchManager, botService) = SetupServices();
        string humanId = "Noah";
        string lobbyId = connectionManager.CreateLobby(humanId);
        connectionManager.AddToLobby(humanId, lobbyId);

        string bot1 = await botService.AddBotAsync(lobbyId);
        string bot2 = await botService.AddBotAsync(lobbyId);
        string bot3 = await botService.AddBotAsync(lobbyId);

        matchManager.StartNewMatch(lobbyId, connectionManager.GetPlayers(lobbyId), new DataInfo());

        Assert.Equal(4, connectionManager.GetPlayers(lobbyId).Count);
        Assert.Equal(3, botService.GetBots(lobbyId).Count);

        // Act - Simulate a Bot winning the game
        connectionManager.RemoveLobby(bot2);

        // Assert
        Assert.Throws<Exception>(() => connectionManager.GetPlayers(lobbyId));
        Assert.Empty(botService.GetBots(lobbyId));
        Assert.False(matchManager.IsMatchActive(lobbyId));
    }

    [Fact]
    public async Task Lobby_ShouldCleanUp_WhenMatchIsTerminatedEarly()
    {
        var (connectionManager, matchManager, botService) = SetupServices();
        string hostId = "Noah";
        string player2 = "Nikola";
        string lobbyId = connectionManager.CreateLobby(hostId);
        connectionManager.AddToLobby(hostId, lobbyId);
        connectionManager.AddToLobby(player2, lobbyId);

        string bot1 = await botService.AddBotAsync(lobbyId);

        matchManager.StartNewMatch(lobbyId, connectionManager.GetPlayers(lobbyId), new DataInfo());

        // Simulate host leaving the game unexpectedly resulting in lobby destruction
        connectionManager.RemoveLobby(hostId);

        Assert.Throws<Exception>(() => connectionManager.GetPlayers(lobbyId));
        Assert.Empty(botService.GetBots(lobbyId));
        Assert.False(matchManager.IsMatchActive(lobbyId));
        Assert.False(connectionManager.IsHost(lobbyId, hostId));
    }
}
