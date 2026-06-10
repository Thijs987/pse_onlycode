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
        int roll = rand.Next(100);
        string resultMessage = "";

        if (roll < 10)
        {
            // Rotate hands to the next player
            var newHands = new Dictionary<string, List<string>>();

            for (int i = 0; i < matchState.PlayerIds.Count; i++)
            {
                string currentPlayer = matchState.PlayerIds[i];
                string nextPlayer = matchState.PlayerIds[(i + 1) % matchState.PlayerIds.Count];

                // If player doesn't exist in hands dict for some reason, create empty list
                var currentHand = matchState.PlayerHands.ContainsKey(currentPlayer)
                    ? matchState.PlayerHands[currentPlayer]
                    : new List<string>();

                newHands[nextPlayer] = new List<string>(currentHand);
            }

            matchState.PlayerHands = newHands;
            resultMessage = $"{playerId} played Miracle Sort and... a miracle happened! Everyone's hands were passed to the next player!";  // 67
        }
        else
        {
            // Shuffle deck
            matchState.Deck = matchState.Deck.OrderBy(_ => rand.Next()).ToList();
            resultMessage = $"{playerId} played Miracle Sort! The deck has been shuffled.";
        }

        return new DataInfo
        {
            CardId = CardId,
            Message = resultMessage
        };
    }
}