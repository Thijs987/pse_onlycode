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

        foreach (string p in players){
            newState.PlayerHands.Add(p, []);
        }

        newState.Deck.AddLast("1");
        newState.Deck.AddLast("2");
        newState.Deck.AddLast("3");
        newState.Deck.AddLast("4");
        newState.Deck.AddLast("5");
        newState.Deck.AddLast("6");
        newState.Deck.AddLast("7");
        newState.Deck.AddLast("8");
        newState.Deck.AddLast("9");
        newState.Deck.AddLast("10");
        newState.Deck.AddLast("11");
        newState.Deck.AddLast("12");
        newState.Deck.AddLast("13");

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
                result.CardId = cardData.CardId;
                break;
            case "SQL":
                result.CardId = cardData.CardId;
                break;
            case "cm":
                result.CardId = cardData.CardId;
                break;
            case "wild":
                result.CardId = cardData.CardId;
                break;
            case "vibe":
                result.CardId = cardData.CardId;
                break;
            case "loop":
                result.CardId = cardData.CardId;
                break;
            case "com":
                result.CardId = cardData.CardId;
                break;
            case "im":
                result.CardId = cardData.CardId;
                break;
            case "os":
                result.CardId = cardData.CardId;
                break;
            case "th":
                result.CardId = cardData.CardId;
                break;
            case "def":
                result.CardId = cardData.CardId;
                break;
            case "ms":
                result.CardId = cardData.CardId;
                break;
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
        var hands = match.PlayerHands;

        string card = firstNode.Value;
        deck.RemoveFirst();
        Console.WriteLine($"The first card is {card}");

        hands[playerId].Add(card);

        Console.WriteLine($"{card} added to {playerId}'s hand");

        if (deck.Count <= 0)
        {
            // Refill deck
            match.CardLimit -=1;
            Console.WriteLine("Deck empty");
        }

        // No top card, not possible
        if (firstNode == null)
        {
            throw new Exception("No top card");
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

    public int CheckCardLimit(string matchId, string playerId)
    {
        // hands.TryGetValue(playerId, out var hand);
        // var count = hand.Count;
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return -1;
        }
        if (!match.PlayerHands.TryGetValue(playerId, out var hand))
        {
            Console.WriteLine($"Cannot find player {playerId}");
            return -1;
        }

        if(hand.Count < match.CardLimit){
            return 0;
        }

        // remove from cycle
        match.PlayerIds.Remove(playerId);
        // remove hand from dict
        match.PlayerHands.Remove(playerId);

        return 1;
    }
}
