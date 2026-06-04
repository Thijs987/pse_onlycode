using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class DDosCard : ICardEffect
{
    public string CardId => "DDos";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        int currentIndex = matchState.PlayerIds.IndexOf(playerId);
        int nextIndex = (currentIndex + 1) % matchState.PlayerIds.Count;
        string nextPlayer = matchState.PlayerIds[nextIndex];

        matchState.CurrentTurnPlayerId = nextPlayer;
        matchState.NTurns += 1;

        matchState.PlayerHands[playerId].Remove(CardId);

        return new DataInfo
        {
            CardId = CardId,
            NextPlayer = nextPlayer,
            Turns = matchState.NTurns,
            Message = $"{playerId} launched a DDos attack! {nextPlayer} must play {matchState.NTurns} turns."
        };
    }
}