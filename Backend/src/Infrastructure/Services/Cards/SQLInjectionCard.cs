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

        matchState.CurrentTurnPlayerId = cardData.Target;
        matchState.NTurns = 2;

        if (matchState.PlayerHands.ContainsKey(playerId))
        {
            matchState.PlayerHands[playerId].Remove(CardId);
        }

        responseData.NextPlayer = cardData.Target;
        responseData.Turns = matchState.NTurns;
        responseData.Message = $"{playerId} launched an SQL Injection! {cardData.Target} must play {matchState.NTurns} turns.";

        return responseData;
    }
}
