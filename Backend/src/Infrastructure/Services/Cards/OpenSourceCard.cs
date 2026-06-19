using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class OpenSourceCard : ICardEffect
{
    public string CardId => "os";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        if (cardData.Target == "view")
        {
            // Look at bottom card
            if (match.Deck.Count == 0) return new DataInfo { Error = "Deck is empty!" };

            if (match.PlayerHands.ContainsKey(playerId))
            {
                match.PlayerHands[playerId].Remove(CardId);
            }

            match.PendingAction = CardId;
            match.PendingActionPlayerId = playerId;

            string bottomCard = match.Deck.Last();

            return new DataInfo
            {
                CardId = CardId,
                Cards = new List<string> { bottomCard },
                IsPrivate = true,
                Message = "Choose 'take' or 'top'"
            };
        }
        else if (cardData.Target == "take" || cardData.Target == "top")
        {
            // Resolve choice
            if (match.PendingAction != CardId || match.PendingActionPlayerId != playerId)
            {
                return new DataInfo { Error = "Not resolving Open Source card!" };
            }

            if (match.Deck.Count == 0) return new DataInfo { Error = "Deck is empty!" };

            string bottomCard = match.Deck.Last();
            match.Deck.RemoveAt(match.Deck.Count - 1);

            if (cardData.Target == "take")
            {
                if (match.PlayerHands.ContainsKey(playerId))
                {
                    match.PlayerHands[playerId].Add(bottomCard);
                }
            }
            else if (cardData.Target == "top")
            {
                match.Deck.Insert(0, bottomCard);
            }

            // Clear pending action
            match.PendingAction = string.Empty;
            match.PendingActionPlayerId = string.Empty;

            return new DataInfo
            {
                CardId = CardId,
                Target = cardData.Target,
                Turns = match.NTurns,
                Message = $"{playerId} resolved Open Source by choosing '{cardData.Target}'"
            };
        }

        return new DataInfo { Error = "Invalid target for Open Source card" };
    }
}
