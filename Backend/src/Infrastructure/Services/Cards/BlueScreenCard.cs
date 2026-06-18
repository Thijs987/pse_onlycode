using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

//request =
//    {
//   "action": "PLAY_CARD",
//   "playerId": "Player_1",
//    "data":
//    {
//     "cardId": "blue"
//    }
//   }

//response =
// {
//   "action": "ERROR",
//   "playerId": "Player_1",
//   "data": {
//     "cardId": "blue",
//     "turns": 1,
//     "error": "Card can only be played after drawing a improved hardware.",
//     "cards": [],
//     "isPrivate": false
//   }
// }

public class BlueScreenCard : ICardEffect
{
    public string CardId => "blue";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        var responseData = new DataInfo{
            CardId = CardId,
            Error = "Card can only be played after drawing a improved hardware."
        };

        return responseData;
    }
}
