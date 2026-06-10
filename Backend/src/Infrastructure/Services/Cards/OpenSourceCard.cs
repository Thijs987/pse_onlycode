using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class OpenSourceCard : ICardEffect
{
    public string CardId => "os";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        if (cardData.Target == "view")
        {
            // Look at bottom card
            if (matchState.Deck.Count == 0) return new DataInfo { Error = "Deck is empty!" };

            if (matchState.PlayerHands.ContainsKey(playerId))
            {
                matchState.PlayerHands[playerId].Remove(CardId);
            }

            matchState.PendingAction = CardId;
            matchState.PendingActionPlayerId = playerId;

            string bottomCard = matchState.Deck.Last();

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
            if (matchState.PendingAction != CardId || matchState.PendingActionPlayerId != playerId)
            {
                return new DataInfo { Error = "Not resolving Open Source card!" };
            }

            if (matchState.Deck.Count == 0) return new DataInfo { Error = "Deck is empty!" };

            string bottomCard = matchState.Deck.Last();
            matchState.Deck.RemoveAt(matchState.Deck.Count - 1);

            if (cardData.Target == "take")
            {
                if (matchState.PlayerHands.ContainsKey(playerId))
                {
                    matchState.PlayerHands[playerId].Add(bottomCard);
                }
            }
            else if (cardData.Target == "top")
            {
                matchState.Deck.Insert(0, bottomCard);
            }

            // Clear pending action
            matchState.PendingAction = string.Empty;
            matchState.PendingActionPlayerId = string.Empty;

            // End turn
            matchState.NTurns--;
            if (matchState.NTurns <= 0)
            {
                int currentIndex = matchState.PlayerIds.IndexOf(playerId);
                int nextIndex = (currentIndex + 1) % matchState.PlayerIds.Count;
                matchState.CurrentTurnPlayerId = matchState.PlayerIds[nextIndex];
                matchState.NTurns = 1;
            }

            return new DataInfo
            {
                CardId = CardId,
                Target = cardData.Target,
                NextPlayer = matchState.CurrentTurnPlayerId,
                Turns = matchState.NTurns,
                Message = $"{playerId} resolved Open Source by choosing '{cardData.Target}'"
            };
        }

        return new DataInfo { Error = "Invalid target for Open Source card" };
    }
}