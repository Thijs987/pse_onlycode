using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain;
using Infrastructure.Services.Cards;
using Xunit;

namespace Backend.Tests;

public class MatchManagerTests
{
    [Fact]
    public void TryPlayCard_ShouldReturnError_WhenTargetIsEliminated()
    {
        var connectionManager = new ConnectionManager();
        var allCards = new List<ICardEffect> { new TrojanCard(), new SQLInjectionCard() };
        var matchManager = new MatchManager(allCards, connectionManager);

        string matchId = "testLobby";
        string player1 = "p1";
        string player2 = "p2";

        var match = matchManager.StartNewMatch(matchId, new List<string> { player1, player2 }, new DataInfo());

        // Manually eliminate player2
        matchManager.RemoveFromMatch(player2, matchId);

        // Ensure p1 is active, it's p1's turn, and they hold the card
        match.CurrentTurnPlayerId = player1;
        match.PlayerHands[player1].Add("trojan");

        var cardData = new DataInfo { CardId = "trojan", Target = player2 };

        var result = matchManager.TryPlayCard(matchId, player1, cardData);

        Assert.NotNull(result);
        Assert.Equal("Target player has been eliminated.", result.Error);
    }
}
