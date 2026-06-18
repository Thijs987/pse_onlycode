using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

//request =
// {
//   "action": "PLAY_CARD",
//   "playerId": "Player_1",
//   "data": {
//     "cardId": "vibe",
//     "target": "Player_2",
//     "cards": ["vibe", "vibe"] <- [always vibe, other blanco card]
//   }
// }

//response target =
// {
//   "action": "CARD_PLAYED",
//   "playerId": "Player_1",
//   "data": {
//     "cardId": "vibe", <- received card from top of deck
//     "target": "Player_2",
//     "turns": 1,
//     "cards": ["vibe", "vibe"], <- [always vibe, other blanco card]
//     "isPrivate": false
//   }
// }

//response for others =
// {
//   "action": "CARD_PLAYED",
//   "playerId": "Player_1",
//   "data": {
//     "target": "Player_2",
//     "turns": 1,
//     "cards": [ "vibe", "vibe"] <- [always vibe, other blanco card]
//     "isPrivate": false
//   }
// }

public class VibeCodingCard : ICardEffect
{
    public string CardId => "vibe";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        // TODO: Write the actual logic
        var responseData = new DataInfo{
            CardId = CardId
        };

        if (!match.PlayerIds.Contains(cardData.Target)){
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

        match.PlayerHands[target].Add(card);

        responseData.Target = target;

        // Standard Cleanup
        if (match.PlayerHands.ContainsKey(playerId))
        {
            foreach(var cards in sendCards) {
                match.PlayerHands[playerId].Remove(cards);
                responseData.Cards.Add(cards);
            }
            responseData.Cards.Add(card);
        }
        return responseData;
    }
}
