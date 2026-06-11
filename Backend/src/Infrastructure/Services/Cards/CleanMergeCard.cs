using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class CleanMergeCard : ICardEffect
{
    // The ID might be placeholder, im not sure tbh
    public string CardId => "cm";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
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
        } else {
            nextPlayer = playerId;
        }

        matchState.PlayerHands[playerId].Remove(CardId);

        return new DataInfo
        {
            CardId = CardId,
            NextPlayer = nextPlayer,
            Turns = match.NTurns,
            Message = $"{playerId} played Clean Merge! Turn skipped."
        };
    }
}
