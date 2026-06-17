using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class ImprovedHardwareCard : ICardEffect
{
    public string CardId => "imp";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        // var responseData = new DataInfo { CardId = CardId, Cards = new List<string> { CardId } };

        // if (matchState.PlayerHands.TryGetValue(playerId, out var hand))
        // {
        //     hand.Remove(CardId);

        //     string? cardToDiscard = null;
        //     if (cardData.Cards != null && cardData.Cards.Count > 0)
        //     {
        //         cardToDiscard = cardData.Cards.FirstOrDefault(c => c != CardId && hand.Contains(c));
        //     }

        //     if (cardToDiscard == null)
        //     {
        //         cardToDiscard = hand.FirstOrDefault(c => c != CardId);
        //     }

        //     if (cardToDiscard != null)
        //     {
        //         hand.Remove(cardToDiscard);
        //         matchState.TableCards.Add(cardToDiscard);
        //         responseData.Cards.Add(cardToDiscard);
        //         responseData.Message = $"{playerId} discarded {CardId} and {cardToDiscard}.";
        //     }
        //     else
        //     {
        //         responseData.Message = $"{playerId} discarded {CardId}.";
        //     }
        // Standard Cleanup
        if (match.PlayerHands.ContainsKey(playerId))
        {
            match.PlayerHands[playerId].Remove(CardId);
        }

        // return responseData;
        return new DataInfo
        {
            CardId = CardId,
            Message = $"{playerId} played {CardId}, but the effect is not yet implemented!"
        };
    }
}
