using System;
using System.Collections.Generic;
using System.Linq;
using Application.Interfaces;
using Domain;
using Xunit;

namespace Backend.Tests;

public class ConnectionManagerTests
{
    [Fact]
    public void RemoveLobby_ShouldCleanUpAllInternalDictionaries()
    {
        // Arrange
        var connectionManager = new ConnectionManager();
        var matchManager = new MatchManager(Array.Empty<ICardEffect>(), connectionManager);

        string hostId = "gobtest";
        string player2 = "gobtest2";

        string lobbyId = connectionManager.CreateLobby(hostId);

        connectionManager.AddToLobby(hostId, lobbyId);
        connectionManager.AddToLobby(player2, lobbyId);

        var players = connectionManager.GetPlayers(lobbyId);
        Assert.Equal(2, players.Count);
        Assert.Contains(hostId, players);
        Assert.Contains(player2, players);
        Assert.True(connectionManager.IsHost(lobbyId, hostId));

        connectionManager.RemoveLobby(hostId);

        // After removal, GetPlayers should throw because the lobby doesn't exist anymore
        Assert.Throws<Exception>(() => connectionManager.GetPlayers(lobbyId));

        // IsHost should return false since the lobby Host mapping was removed
        Assert.False(connectionManager.IsHost(lobbyId, hostId));

        // If we try to create another lobby, it shouldn't conflict, and connection To Lobby mappings should be cleared.
        // Also the lobby shouldn't appear in GetActiveLobbies
        var activeLobbies = connectionManager.GetActiveLobbies(matchManager);
        Assert.Empty(activeLobbies);
    }
}
