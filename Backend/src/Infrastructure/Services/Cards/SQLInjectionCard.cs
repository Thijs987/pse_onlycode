using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class SQLInjectionCard : ICardEffect
{
    public string CardId => "sql";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        // Standard Cleanup
        if (matchState.PlayerHands.ContainsKey(playerId))
        {
            matchState.PlayerHands[playerId].Remove(CardId);
        }

        // TODO: Write the actual logic
        var target = cardData.Target;
        matchState.CurrentTurnPlayerId = target;
        matchState.NTurns = 2;

        matchState.PlayerHands[playerId].Remove(CardId);

        return new DataInfo
        {
            CardId = CardId,
            NextPlayer = cardData.Target,
            Turns = 2,
            Message = $"{playerId} launched a DDos attack! {target} must play {matchState.NTurns} turns."
        };
    }
}
