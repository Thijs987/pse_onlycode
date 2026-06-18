using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

//request =
//    {
//   "action": "PLAY_CARD",
//   "playerId": "Player_1",
//    "data":
//    {
//     "cardId": "merge"
//    }
//   }

//response =
// {
//   "action": "ERROR",
//   "playerId": "Player_1",
//   "data": {
//     "cardId": "merge",
//     "turns": 1,
//     "error": "Card can only be played after drawing a improved hardware.",
//     "cards": [],
//     "isPrivate": false
//   }
// }


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
