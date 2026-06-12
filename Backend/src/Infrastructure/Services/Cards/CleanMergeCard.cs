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
            // Advance the turn to the next player if current has none
            int currentIndex = matchState.PlayerIds.IndexOf(playerId);
            int nextIndex = (currentIndex + 1) % matchState.PlayerIds.Count;
            matchState.CurrentTurnPlayerId = matchState.PlayerIds[nextIndex];
            nextPlayer = matchState.PlayerIds[nextIndex];
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
