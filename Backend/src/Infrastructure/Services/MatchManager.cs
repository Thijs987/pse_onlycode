/*
    The MatchManager handles the server-side state of the matches.
    It does NOT send any form of messages to the player(s).
    This is done by the ConnectionManager and called by the MessageRouter.
*/
using System.Collections.Concurrent;

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

    // Returns true if the move was legal, false if it was invalid
    public bool TryPlayCard(string matchId, string playerId, DataInfo cardData)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
            return false;

        if (match.CurrentTurnPlayerId != playerId)
        {
            Console.WriteLine($"{playerId} tried to play out of turn!");
            return false;
        }

        // Apply the game rules
        // var (success, ding) = TryEffectCard(cardData);
        // if(success) {
        //     Console.WriteLine($"{playerId} played: {Data.CardId}");
        // }

        match.TableCards.Add(cardData.CardId);
        return true;
    }

    // apply game effects
    public (bool, string) TryEffectCard(string cardData){
        switch(cardData)
        {
            case "nor":
                //return (false, "reason for invalid")
                return (true, "succesfull play normal");
            case "DDos":
                return (true, "successfull play");
            case "SQL":
                return (true, "successfull play");
            case "cm":
                return (true, "successfull play");
            case "wild":
                return (true, "successfull play");
            case "vibe":
                return (true, "successfull play");
            case "loop":
                return (true, "successfull play");
            case "com":
                return (true, "successfull play");
            case "im":
                return (true, "successfull play");
            case "os":
                return (true, "successfull play");
            case "th":
                return (true, "successfull play");
            case "def":
                return (true, "successfull play");
            case "ms":
                return (true, "successfull play");
            default:
                return (false, "invalid card");
        }
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
