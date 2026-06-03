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

        // TODO: Give initial hands to players

        _activeMatches.TryAdd(matchId, newState);
        Console.WriteLine($"Match {matchId} started!");
    }

    // Returns true if the move was legal, false if it was invalid
    public bool TryPlayCard(string matchId, string playerId, string cardId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
            return false;

        if (match.CurrentTurnPlayerId != playerId)
        {
            Console.WriteLine($"{playerId} tried to play out of turn!");
            return false;
        }

        // Apply the game rules
        match.TableCards.Add(cardId);

        // Advance the turn to the next player
        int currentIndex = match.PlayerIds.IndexOf(playerId);
        int nextIndex = (currentIndex + 1) % match.PlayerIds.Count;
        match.CurrentTurnPlayerId = match.PlayerIds[nextIndex];

        return true;
    }
}