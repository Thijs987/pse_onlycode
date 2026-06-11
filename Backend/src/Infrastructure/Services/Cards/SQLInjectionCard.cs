using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class SQLInjectionCard : ICardEffect
{
    public string CardId => "sql";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        // TODO: Write the actual logic
        var responseData = new DataInfo{
            CardId = CardId
        };

        if (!matchState.PlayerIds.Contains(cardData.Target)){
            responseData.Error = $"{cardData.Target} not in game.";
            return responseData;
        }

        matchState.NTurns = 2;
        matchState.PlayerHands[playerId].Remove(CardId);

        // Standard Cleanup
        if (matchState.PlayerHands.ContainsKey(playerId))
        {
            matchState.PlayerHands[playerId].Remove(CardId);
        }

        responseData.NextPlayer = cardData.Target;
        responseData.Turns = matchState.NTurns;
        responseData.Message = $"{playerId} launched a DDos attack! {cardData.Target} must play {matchState.NTurns} turns.";

        return responseData;
    }
}
