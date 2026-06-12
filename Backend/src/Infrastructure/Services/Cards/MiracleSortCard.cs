using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

// MVP ahh card
public class MiracleSortCard : ICardEffect
{
    public string CardId => "miracle";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        // Standard Cleanup
        if (matchState.PlayerHands.ContainsKey(playerId))
        {
            matchState.PlayerHands[playerId].Remove(CardId);
        }

        var rand = new Random();
        
        // Shuffle deck
        matchState.Deck = matchState.Deck.OrderBy(_ => rand.Next()).ToList();
        string resultMessage = $"{playerId} played Miracle Sort! The deck has been shuffled.";

        return new DataInfo
        {
            CardId = CardId,
            Message = resultMessage
        };
    }
}