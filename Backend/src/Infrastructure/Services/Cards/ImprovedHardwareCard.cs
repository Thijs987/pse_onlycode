using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class ImprovedHardwareCard : ICardEffect
{
    public string CardId => "imp";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        var responseData = new DataInfo { CardId = CardId, Cards = new List<string> { CardId } };

        if (matchState.PlayerHands.TryGetValue(playerId, out var hand))
        {
            hand.Remove(CardId);

            string? cardToDiscard = null;
            if (cardData.Cards != null && cardData.Cards.Count > 0)
            {
                cardToDiscard = cardData.Cards.FirstOrDefault(c => c != CardId && hand.Contains(c));
            }

            if (cardToDiscard == null)
            {
                cardToDiscard = hand.FirstOrDefault(c => c != CardId);
            }

            if (cardToDiscard != null)
            {
                hand.Remove(cardToDiscard);
                matchState.TableCards.Add(cardToDiscard);
                responseData.Cards.Add(cardToDiscard);
                responseData.Message = $"{playerId} discarded {CardId} and {cardToDiscard}.";
            }
            else
            {
                responseData.Message = $"{playerId} discarded {CardId}.";
            }
        }

        return responseData;
    }
}