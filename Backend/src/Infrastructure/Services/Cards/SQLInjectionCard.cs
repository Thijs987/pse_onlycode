using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class SQLInjectionCard : ICardEffect
{
    public string CardId => "sql";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        // TODO: Write the actual logic
        var responseData = new DataInfo{
            CardId = CardId
        };

        if (!match.PlayerIds.Contains(cardData.Target)){
            responseData.Error = $"{cardData.Target} not in game.";
            return responseData;
        }

        match.NTurns = 2;
        match.PlayerHands[playerId].Remove(CardId);

        // Standard Cleanup
        if (match.PlayerHands.ContainsKey(playerId))
        {
            match.PlayerHands[playerId].Remove(CardId);
        }

        responseData.NextPlayer = cardData.Target;
        responseData.Turns = match.NTurns;
        responseData.Message = $"{playerId} launched a DDos attack! {cardData.Target} must play {match.NTurns} turns.";

        return responseData;
    }
}
