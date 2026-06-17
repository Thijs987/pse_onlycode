using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

//request =
//    {
//   "action": "PLAY_CARD",
//   "playerId": "Player_1",
//    "data":
//    {
//     "cardId": "ddos"
//    }
//   }

//response =
// {
//   "action": "CARD_PLAYED",
//   "playerId": "Player_1",
//   "data": {
//     "cardId": "ddos",
//     "message": "Player_1 launched a DDos attack! Player_3 must play 2 turns.",
//     "nextPlayer": "Player_3",
//     "turns": 2,
//     "cards": [],
//     "isPrivate": false
//   }
// }

public class DDosCard : ICardEffect
{
    public string CardId => "ddos";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        var currentCycle = match.PlayerIds
            .Where (id =>
                match.PlayerStatuses.TryGetValue(id, out var status) &&
                status == PlayerStatus.Active || status == PlayerStatus.DisconnectedActive).ToList();

        // Advance the turn to the next player if current has none
        int currentIndex = currentCycle. IndexOf(playerId);
        int nextIndex = (currentIndex + 1) % currentCycle.Count;
        string nextPlayer = currentCycle[nextIndex];
        match.CurrentTurnPlayerId = currentCycle[nextIndex];

        match.CurrentTurnPlayerId = nextPlayer;
        if(match.NTurns == 1) {
            match.NTurns = 2;
        } else {
            match.NTurns += 2;
        }

        match.PlayerHands[playerId].Remove(CardId);

        return new DataInfo
        {
            CardId = CardId,
            NextPlayer = nextPlayer,
            Turns = match.NTurns,
            Message = $"{playerId} launched a DDos attack! {nextPlayer} must play {match.NTurns} turns."
        };
    }
}
