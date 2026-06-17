using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class MergeConflictCard : ICardEffect
{
    public string CardId => "merge";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        // Standard Cleanup
        var responseData = new DataInfo{
            CardId = CardId,
            Error = "Card can only be played after drawing a improved hardware."
        };

        return responseData;
    }
}
