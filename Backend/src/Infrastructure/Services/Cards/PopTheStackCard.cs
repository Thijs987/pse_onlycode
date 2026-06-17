using Application.Interfaces;
using Domain;

namespace Infrastructure.Services.Cards;

public class PopTheStackCard : ICardEffect
{
    public string CardId => "pop";

    public DataInfo ApplyEffect(GameState match, string playerId, DataInfo cardData)
    {
        // Standard Cleanup
        if (match.PlayerHands.ContainsKey(playerId))
        {
            match.PlayerHands[playerId].Remove(CardId);
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
