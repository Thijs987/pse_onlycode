using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class BlueScreenCard : ICardEffect
{
    public string CardId => "blue";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        var responseData = new DataInfo{
            CardId = CardId,
            Error = "Card can only be played after drawing a improved hardware."
        };

        return responseData;
    }
}
