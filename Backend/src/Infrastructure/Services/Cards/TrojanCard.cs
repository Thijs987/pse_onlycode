using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class TrojanCard : ICardEffect
{
    public string CardId => "trojan";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        // TODO: Write the actual logic
        var target = cardData.Target;
        var card = cardData.Cards;
        var responseData = new DataInfo{
            CardId = CardId
        };

        // if players hand contains card send it to the other player.
        if(matchState.PlayerHands[playerId].Contains(card[0])){
            matchState.PlayerHands[target].Add(card[0]);
            matchState.PlayerHands[playerId].Remove(card[0]);
        } else {
            responseData.Error = $"{playerId} doesn't have {card} in hand";
        }

        // Standard Cleanup
        if (matchState.PlayerHands.ContainsKey(playerId))
        {
            matchState.PlayerHands[playerId].Remove(CardId);
        }
        // Return a basic response so the game doesn't crash
        return responseData;
    }
}
