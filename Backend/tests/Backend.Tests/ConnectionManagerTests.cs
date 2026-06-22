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
        var matchManager = new MatchManager(new List<ICardEffect>());
        
        string hostId = "host123";
        string player2 = "player456";
        
        // Create a lobby via HTTP logic
        string lobbyId = connectionManager.CreateLobby(hostId);
        
        // Add connections to the lobby
        connectionManager.AddToLobby(hostId, lobbyId);
        connectionManager.AddToLobby(player2, lobbyId);
        
        // Assert initial state
        var players = connectionManager.GetPlayers(lobbyId);
        Assert.Equal(2, players.Count);
        Assert.Contains(hostId, players);
        Assert.Contains(player2, players);
        Assert.True(connectionManager.IsHost(lobbyId, hostId));
        
        // Act - Call RemoveLobby using one of the connection IDs
        connectionManager.RemoveLobby(hostId, matchManager);
        
        // Assert
        // After removal, GetPlayers should throw because the lobby doesn't exist anymore
        Assert.Throws<Exception>(() => connectionManager.GetPlayers(lobbyId));
        
        // IsHost should return false since the lobby Host mapping was removed
        Assert.False(connectionManager.IsHost(lobbyId, hostId));
        
        // If we try to create another lobby, it shouldn't conflict, and connection To Lobby mappings should be cleared.
        // We can verify this implicitly by seeing if RemoveLobby on player2 does nothing or if adding player2 elsewhere works without old mappings polluting.
        // Also the lobby shouldn't appear in GetActiveLobbies
        var activeLobbies = connectionManager.GetActiveLobbies(matchManager);
        Assert.Empty(activeLobbies);
    }
}
