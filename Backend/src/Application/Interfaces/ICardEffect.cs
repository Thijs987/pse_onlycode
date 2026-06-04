using Domain;

namespace Application.Interfaces;

public interface ICardEffect
{
    // For example: "DDos", "SQL"
    string CardId { get; }

    // Applies the effect directly to the GameState and returns the response for the clients
    DataInfo ApplyEffect(GameState matchState, string playerId, DataInfo cardData);
}