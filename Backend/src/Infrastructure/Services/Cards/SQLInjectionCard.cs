using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

//request =
// {
//   "action": "PLAY_CARD",
//   "playerId": "Player_1",
//   "data": {
//     "cardId": "sql",
//     "target": "Player_2"
//   }
// }

//response =
// {
//   "action": "CARD_PLAYED",
//   "playerId": "Player_1",
//   "data": {
//     "cardId": "sql",
//     "message": "Player_1 launched a DDos attack! Player_2 must play 2 turns.",
//     "nextPlayer": "Player_2",
//     "turns": 2,
//     "cards": [],
//     "isPrivate": false
//   }
// }

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

        if(match.NTurns == 1) {
            match.NTurns = 2;
        } else {
            match.NTurns += 2;
        }
        match.PlayerHands[playerId].Remove(CardId);
        match.CurrentTurnPlayerId = cardData.Target;

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
