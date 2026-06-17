using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class CleanMergeCard : ICardEffect
{
    // The ID might be placeholder, im not sure tbh
    public string CardId => "cm";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        match.NTurns--;
        var nextPlayer = playerId;

        // Attack card can cause NTurns > 1
        if (match.NTurns <= 0)
        {
            var currentCycle = match.PlayerIds
            .Where (id =>
                match.PlayerStatuses.TryGetValue(id, out var status) &&
                status == PlayerStatus.Active || status == PlayerStatus.DisconnectedActive).ToList();
            // Advance the turn to the next player if current has none
            int currentIndex = currentCycle. IndexOf(playerId);
            int nextIndex = (currentIndex + 1) % currentCycle.Count;
            match.CurrentTurnPlayerId = currentCycle[nextIndex];
            // Set NTrns to 1
            match.NTurns = 1;
        }

        match.PlayerHands[playerId].Remove(CardId);

        return new DataInfo
        {
            CardId = CardId,
            NextPlayer = nextPlayer,
            Turns = match.NTurns,
            Message = $"{playerId} played Clean Merge! Turn skipped."
        };
    }
}
