using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class CleanMergeCard : ICardEffect
{
    // The ID might be placeholder, im not sure tbh
    public string CardId => "cm";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        int currentIndex = matchState.PlayerIds.IndexOf(playerId);
        int nextIndex = (currentIndex + 1) % matchState.PlayerIds.Count;
        string nextPlayer = matchState.PlayerIds[nextIndex];

        matchState.CurrentTurnPlayerId = nextPlayer;
        matchState.NTurns = 1;

        matchState.PlayerHands[playerId].Remove(CardId);

        return new DataInfo
        {
            CardId = CardId,
            NextPlayer = nextPlayer,
            Turns = 1,
            Message = $"{playerId} played Clean Merge! Turn skipped."
        };
    }
}