using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class CleanMergeCard : ICardEffect
{
    // The ID might be placeholder, im not sure tbh
    public string CardId => "cm";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        matchState.NTurns--;
        var nextPlayer = playerId;

        // Attack card can cause NTurns > 1
        if (matchState.NTurns <= 0)
        {
            var currentCycle = matchState.PlayerIds
            .Where (id =>
                matchState.PlayerStatuses.TryGetValue(id, out var status) &&
                status == PlayerStatus.Active || status == PlayerStatus.DisconnectedActive).ToList();
            // Advance the turn to the next player if current has none
            int currentIndex = currentCycle. IndexOf(playerId);
            int nextIndex = (currentIndex + 1) % currentCycle.Count;
            matchState.CurrentTurnPlayerId = currentCycle[nextIndex];
            // Set NTrns to 1
            matchState.NTurns = 1;
        }

        matchState.PlayerHands[playerId].Remove(CardId);

        return new DataInfo
        {
            CardId = CardId,
            NextPlayer = nextPlayer,
            Turns = matchState.NTurns,
            Message = $"{playerId} played Clean Merge! Turn skipped."
        };
    }
}
