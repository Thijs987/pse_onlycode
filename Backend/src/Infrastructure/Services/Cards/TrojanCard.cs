using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class TrojanCard : ICardEffect
{
    public string CardId => "trojan";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        // TODO: Write the actual logic
        var target = cardData.Target;
        var card = cardData.Cards;
        var responseData = new DataInfo{
            CardId = CardId,
            Target = target
        };

        if(card.Count != 1) {
            responseData.Error = "No card assigned";
            return responseData;
        }

        // if players hand contains card send it to the other player.
        if(match.PlayerHands[playerId].Contains(card[0])){
            match.PlayerHands[target].Add(card[0]);
            match.PlayerHands[playerId].Remove(card[0]);
            responseData.Cards.Add(card[0]);
        } else {
            responseData.Error = $"{playerId} doesn't have {card} in hand";
        }

        // Standard Cleanup
        if (match.PlayerHands.ContainsKey(playerId))
        {
            match.PlayerHands[playerId].Remove(CardId);
        } else {
            match.PlayerHands[target].Remove(card[0]);
            match.PlayerHands[playerId].Add(card[0]);
            responseData.Error = $"{playerId} doesn't have {card} in hand";
        }
        // Return a basic response so the game doesn't crash
        return responseData;
    }
}
