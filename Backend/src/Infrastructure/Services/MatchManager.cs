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

    public void StartNewMatch(string matchId, List<string> players)
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
    }

    // Returns DataInfo if there is no error result.Error=="".
    // Otherwise specific error is is inside result.Error.
    public DataInfo TryPlayCard(string matchId, string playerId, DataInfo cardData)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
            return new DataInfo {Error = "Match not found!"};

        if (match.CurrentTurnPlayerId != playerId)
        {
            Console.WriteLine($"{playerId} tried to play out of turn!");
            return new DataInfo {Error = "Tried to play out of turn!"};
        }

        // Apply the game rules
        var result = TryEffectCard(cardData);
        if(result.Error == "") {
            match.TableCards.Add(cardData.CardId);
        }
        return result;
    }

    // apply game effects
    public DataInfo TryEffectCard(DataInfo cardData){
        var result = new DataInfo();
        switch(cardData.CardId)
        {
            case "nor":
                result.CardId = cardData.CardId;
                break;
            case "DDos":
            case "SQL":
            case "cm":
            case "wild":
            case "vibe":
            case "loop":
            case "com":
            case "im":
            case "os":
            case "th":
            case "def":
            case "ms":
            default:
                return new DataInfo {Error = "Invalid card"};
        }
        return result;
    }

    public string GetFirstCard(string matchId, string playerId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return "";
        }

        if (match.CurrentTurnPlayerId != playerId)
        {
            Console.WriteLine($"{playerId} tried to draw, but not their turn!");
            return "";
        }

        var deck = match.Deck;
        var firstNode = deck.First;

        // No top card, not possible
        if (firstNode == null)
        {
            throw new Exception("No top card");
        }

        string card = firstNode.Value;
        deck.RemoveFirst();
        Console.WriteLine($"The first card is {card}");

        if (deck.Count <= 0)
        {
            // Refill deck
            Console.WriteLine("Deck empty");
        }

        return card;
    }

    public (string, int) NextTurn(string matchId, string playerId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return ("", -1);
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

        return (match.CurrentTurnPlayerId, match.NTurns);
    }
}
