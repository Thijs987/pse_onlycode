/*
    The MatchManager handles the server-side state of the matches.
    It does NOT send any form of messages to the player(s).
    This is done by the ConnectionManager and called by the MessageRouter.
*/
using System.Collections.Concurrent;
using Domain;

public class MatchManager
{
    private readonly ConcurrentDictionary<string, GameState> _activeMatches = new();

    public DataInfo StartNewMatch(string matchId, List<string> players)
    {
        var newState = new GameState
        {
            MatchId = matchId,
            PlayerIds = players,
            CurrentTurnPlayerId = players[0]
        };

        newState.Deck.AddLast("1");
        newState.Deck.AddLast("2");
        newState.Deck.AddLast("3");

        // TODO: Give initial hands to players

        _activeMatches.TryAdd(matchId, newState);

        Console.WriteLine($"Match {matchId} started!");

        return new DataInfo { NextPlayer = newState.CurrentTurnPlayerId };
    }

    // Returns DataInfo if there is no error responseData.Error=="".
    // Otherwise specific error is is inside responseData.Error.
    public DataInfo TryPlayCard(string matchId, string playerId, DataInfo cardData)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
            return new DataInfo { Error = "Match not found!" };

        if (match.CurrentTurnPlayerId != playerId)
        {
            Console.WriteLine($"{playerId} tried to play out of turn!");
            return new DataInfo { Error = "Tried to play out of turn!" };
        }

        // Apply the game rules
        var responseData = TryEffectCard(cardData);
        if (responseData.Error == "")
        {
            match.TableCards.Add(cardData.CardId);
        }
        return responseData;
    }

    // apply game effects
    public DataInfo TryEffectCard(DataInfo cardData)
    {
        var responseData = new DataInfo();
        switch (cardData.CardId)
        {
            case "nor":
                responseData.CardId = cardData.CardId;
                break;
            case "DDos":
                responseData.CardId = cardData.CardId;
                break;
            case "SQL":
                responseData.CardId = cardData.CardId;
                break;
            case "cm":
                responseData.CardId = cardData.CardId;
                break;
            case "wild":
                responseData.CardId = cardData.CardId;
                break;
            case "vibe":
                responseData.CardId = cardData.CardId;
                break;
            case "loop":
                responseData.CardId = cardData.CardId;
                break;
            case "com":
                responseData.CardId = cardData.CardId;
                break;
            case "im":
                responseData.CardId = cardData.CardId;
                break;
            case "os":
                responseData.CardId = cardData.CardId;
                break;
            case "th":
                responseData.CardId = cardData.CardId;
                break;
            case "def":
                responseData.CardId = cardData.CardId;
                break;
            case "ms":
                responseData.CardId = cardData.CardId;
                break;
            default:
                return new DataInfo { Error = "Invalid card" };
        }
        return responseData;
    }

    public DataInfo GetFirstCard(string matchId, string playerId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return new DataInfo { Error = $"Cannot find match {matchId}" };
        }

        if (match.CurrentTurnPlayerId != playerId)
        {
            Console.WriteLine($"{playerId} tried to draw, but not their turn!");
            return new DataInfo { Error = $"Cannot find player {playerId}" };
        }

        var deck = match.Deck;
        var firstNode = deck.First;

        // No top card, not possible
        if (firstNode == null)
        {
            return new DataInfo { Error = $"No top card" };
        }

        string card = firstNode.Value;
        deck.RemoveFirst();
        Console.WriteLine($"The first card is {card}");

        if (deck.Count <= 0)
        {
            // Refill deck
            Console.WriteLine("Deck empty");
        }

        var responseData = new DataInfo { CardId = card };

        return responseData;
    }

    public DataInfo NextTurn(string matchId, string playerId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return new DataInfo { Error = $"Cannot find match {matchId}" };
        }

        match.NTurns--;

        // Attack card can cause NTurns > 1
        if (match.NTurns <= 0)
        {
            // Advance the turn to the next player if current has none
            int currentIndex = match.PlayerIds.IndexOf(playerId);
            int nextIndex = (currentIndex + 1) % match.PlayerIds.Count;
            match.CurrentTurnPlayerId = match.PlayerIds[nextIndex];

            // Set NTrns to 1
            match.NTurns = 1;
        }

        var responseData = new DataInfo
        {
            NextPlayer = match.CurrentTurnPlayerId,
            Turns = match.NTurns
        };

        return responseData;
    }
}
