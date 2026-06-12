using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class ErrorCard : ICardEffect
{
    public string CardId => "err";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        // Standard Cleanup
        var responseData = new DataInfo{
            CardId = CardId,
            Error = "Card can only be played after drawing a improved hardware."
        };

        return responseData;
    }
}
