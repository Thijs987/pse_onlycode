using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

// MVP ahh card
public class MiracleSortCard : ICardEffect
{
    public string CardId => "miracle";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        // Standard Cleanup
        if (match.PlayerHands.ContainsKey(playerId))
        {
            match.PlayerHands[playerId].Remove(CardId);
        }

        var rand = new Random();
        
        // Shuffle deck
        match.Deck = match.Deck.OrderBy(_ => rand.Next()).ToList();
        string resultMessage = $"{playerId} played Miracle Sort! The deck has been shuffled.";

        return new DataInfo
        {
            CardId = CardId,
            Message = resultMessage
        };
    }
}
