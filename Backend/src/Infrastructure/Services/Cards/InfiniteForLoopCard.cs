using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class InfiniteForLoopCard : ICardEffect
{
    public string CardId => "inf";

    public DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData)
    {
        // Standard Cleanup
        if (matchState.PlayerHands.ContainsKey(playerId))
        {
            matchState.PlayerHands[playerId].Remove(CardId);
        }

        // TODO: Write the actual logic

        // Return a basic response so the game doesn't crash
        return new DataInfo
        {
            CardId = CardId,
            Message = $"{playerId} played {CardId}, but the effect is not yet implemented!"
        };
    }
}