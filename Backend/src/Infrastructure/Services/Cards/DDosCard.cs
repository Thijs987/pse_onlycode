using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class DDosCard : ICardEffect
{
    public string CardId => "ddos";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        var currentCycle = matchState.PlayerIds
            .Where (id =>
                matchState.PlayerStatuses.TryGetValue(id, out var status) &&
                status == PlayerStatus.Active || status == PlayerStatus.DisconnectedActive).ToList();

        // Advance the turn to the next player if current has none
        int currentIndex = currentCycle. IndexOf(playerId);
        int nextIndex = (currentIndex + 1) % currentCycle.Count;
        string nextPlayer = currentCycle[nextIndex];
        matchState.CurrentTurnPlayerId = currentCycle[nextIndex];

        matchState.CurrentTurnPlayerId = nextPlayer;
        if(matchState.NTurns == 1) {
            matchState.NTurns = 2;
        } else {
            matchState.NTurns += 2;
        }

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
