using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class VibeCodingCard : ICardEffect
{
    public string CardId => "vibe";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        // TODO: Write the actual logic
        var responseData = new DataInfo{
            CardId = CardId
        };

        if (!matchState.PlayerIds.Contains(cardData.Target)){
            responseData.Error = $"{cardData.Target} not in game.";
            return responseData;
        }

        // TODO: Write the actual logic
        var target = cardData.Target;
        var sendCards = cardData.Cards;
        List <string> blank_cards = new List <string> {"goto", "vibe"};

        //check if enough cards are send
        if(sendCards.Count != 2) {
            responseData.Error = $"incorrect number of cards have been used.";
            return responseData;
        }

        //check if both cards are valid
        foreach (var cards in sendCards) {
            if(!matchState.PlayerHands[playerId].Contains(cards) || !blank_cards.Contains(cards)){
                responseData.Error = $"Incorrect set of cards have been send";
                return responseData;
            }
        }

        //generate and shuffle deck if empty
        if (matchState.Deck.Count <= 0)
        {
            // Refill deck
            Console.WriteLine("Deck empty");
            matchState.Deck = new List<string>(matchState.TableCards);
            matchState.TableCards = [];
            var rand = new Random();
            matchState.Deck = matchState.Deck.OrderBy(_ => rand.Next()).ToList();
            responseData.Message = "deck regenarated";
        }

        // No top card, not possible
        if (matchState.Deck.Count <= 0)
        {
            responseData.Error = "Deck could not be generated";
            return responseData;
        }
        //give card to player.
        var card = matchState.Deck[0];

        matchState.Deck.RemoveAt(0);
        Console.WriteLine($"The first card is {card}");

        matchState.PlayerHands[target].Add(card);

        responseData.Target = target;
        responseData.Message = card;

        // Standard Cleanup
        if (matchState.PlayerHands.ContainsKey(playerId))
        {
            foreach(var cards in sendCards) {
                responseData.Cards.Add(cards);
                matchState.PlayerHands[playerId].Remove(cards);
            }
        }
        return responseData;
    }
}
