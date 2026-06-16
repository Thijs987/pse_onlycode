using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class GoToCard : ICardEffect
{
    public string CardId => "goto";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        var responseData = new DataInfo{
            CardId = CardId
        };

        if (!match.PlayerIds.Contains(cardData.Target)){
            responseData.Error = $"{cardData.Target} not in game.";
            return responseData;
        }

        // TODO: Write the actual logic
        var target = cardData.Target;
        var send_cards = cardData.Cards;
        List <string> blank_cards = new List <string> {"goto", "vibe", "inf", "nocom"};

        //check if enough cards are send
        if(send_cards.Count != 2) {
            responseData.Error = $"incorrect number of cards have been used.";
            return responseData;
        }

        //check if both cards are valid
        foreach (var cards in send_cards) {
            if(!match.PlayerHands[playerId].Contains(cards) || !blank_cards.Contains(cards)){
                responseData.Error = $"Incorrect set of cards have been send";
                return responseData;
            }
        }

        //generate and shuffle deck if empty
        if (match.Deck.Count <= 0)
        {
            // Refill deck
            Console.WriteLine("Deck empty");
            match.Deck = new List<string>(match.TableCards);
            match.TableCards = [];
            var rand = new Random();
            match.Deck = match.Deck.OrderBy(_ => rand.Next()).ToList();
            responseData.Message = "deck regenarated";
        }

        // No top card, not possible
        if (match.Deck.Count <= 0)
        {
            responseData.Error = "Deck could not be generated";
            return responseData;
        }

        //give card to player.
        var card = match.Deck[0];

        match.Deck.RemoveAt(0);
        Console.WriteLine($"The first card is {card}");

        match.PlayerHands[target].Add(card);

        responseData.Target = target;
        responseData.Cards.Add(card);

        // Standard Cleanup
        if (match.PlayerHands.ContainsKey(playerId))
        {
            foreach(var cards in send_cards) {
                match.PlayerHands[playerId].Remove(cards);
            }
        }
        return responseData;
    }
}
