/*
    The MatchManager handles the server-side state of the matches.
    It does NOT send any form of messages to the player(s).
    This is done by the ConnectionManager and called by the MessageRouter.
*/
using System.Collections.Concurrent;
using Domain;
using Application.Interfaces;

public class MatchManager
{
    private const int initialHandSize = 3;

    private readonly ConcurrentDictionary<string, GameState> _activeMatches = new();
    private readonly Dictionary<string, ICardEffect> _cardRegistry = new();

    public MatchManager(IEnumerable<ICardEffect> allCards)
    {
        foreach (var card in allCards)
        {
            _cardRegistry[card.CardId] = card;
        }
    }

    public DataInfo StartNewMatch(string matchId, List<string> players)
    {
        var newState = new GameState
        {
            MatchId = matchId,
            PlayerIds = players,
            CurrentTurnPlayerId = players[0]
        };

        var allCards = new Dictionary<string, int>()
        {
            {"blue", 1},
            {"cm", 1},
            {"ddos", 1},
            {"err", 1},
            {"garb", 1},
            {"goto", 1},
            {"imp", 1},
            {"inf", 1},
            {"merge", 1},
            {"miracle", 1},
            {"nocom", 1},
            {"sql", 1},
            {"trojan", 1},
            {"vibe", 1}
        };

        foreach (var card in allCards)
        {
            for (int i = 0; i < card.Value; i++)
            {
                newState.TableCards.Add(card.Key);
            }
        }

        GenerateDeck(newState);

        // Initialize hands and deal 3 cards per player
        foreach (var player in players)
        {
            newState.PlayerHands[player] = new List<string>();

            for (int i = 0; i < initialHandSize; i++)
            {
                if (newState.Deck.Count > 0)
                {
                    string card = newState.Deck[0];
                    newState.Deck.RemoveAt(0);
                    newState.PlayerHands[player].Add(card);
                }
            }
        }

        _activeMatches.TryAdd(matchId, newState);
        Console.WriteLine($"Match {matchId} started! Handed out initial hands HAHAHAHAHA.");

        return new DataInfo { NextPlayer = newState.CurrentTurnPlayerId };
    }

    //creates a new deck based on the Tablecards
    void GenerateDeck(GameState state)
    {
        state.Deck = new List<string>(state.TableCards);
        state.TableCards = [];
        Randomize(state);
    }

    //Randomizes the deck
    void Randomize(GameState state)
    {
        var rand = new Random();
        state.Deck = state.Deck.OrderBy(_ => rand.Next()).ToList();
        state.Deck.ForEach(Console.WriteLine);
    }

    // Returns DataInfo if there is no error responseData.Error=="".
    // Otherwise specific error is is inside responseData.Error.
    public DataInfo TryPlayCard(string matchId, string playerId, DataInfo cardData)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
            return new DataInfo { Error = "Match not found!" };

        if (!match.PlayerHands.TryGetValue(playerId, out var hands))
        {
            return new DataInfo { Error = "Cannot find player {playerId}" };
        }

        if (!match.PlayerHands[playerId].Contains(cardData.CardId))
        {
            Console.WriteLine($"{playerId} tried to play a card they don't have: {cardData.CardId}");
            return new DataInfo { Error = "You do not have that card in your hand!" };
        }

        // Look up the card in registry
        if (!_cardRegistry.TryGetValue(cardData.CardId, out var cardEffect))
        {
            return new DataInfo { Error = $"Unknown card: {cardData.CardId}" };
        }

        // Apply the specific card's logic
        var responseData = cardEffect.ApplyEffect(match, playerId, cardData);

        // If no errors, add it to the table
        if (string.IsNullOrEmpty(responseData.Error))
        {
            match.TableCards.Add(cardData.CardId);
        }

        return responseData;
    }

    // Legendary Artifact

    // apply game effects
    // public DataInfo TryEffectCard(DataInfo cardData)
    // {
    //     var responseData = new DataInfo();
    //     switch (cardData.CardId)
    //     {
    //         case "nor":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "DDos":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "SQL":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "cm":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "wild":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "vibe":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "loop":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "com":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "im":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "os":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "th":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "def":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         case "ms":
    //             responseData.CardId = cardData.CardId;
    //             break;
    //         default:
    //             return new DataInfo { Error = "Invalid card" };
    //     }
    //     return responseData;
    // }

    public DataInfo GetFirstCard(string matchId, string playerId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return new DataInfo { Error = $"Cannot find match {matchId}" };
        }

        if (match.CurrentTurnPlayerId != playerId)
        {
            Console.WriteLine($"{playerId} tried to draw, but not their turn!");
            return new DataInfo { Error = $"Not your turn" };
        }

        var responseData = new DataInfo { };

        if (match.Deck.Count <= 0)
        {
            // Refill deck
            Console.WriteLine("Deck empty");
            GenerateDeck(match);
            responseData.Message = "1";
        }

        // No top card, not possible
        if (match.Deck.Count <= 0)
        {
            return new DataInfo { Error = "Deck could not be generated" };
        }

        var card = match.Deck[0];

        match.Deck.RemoveAt(0);
        Console.WriteLine($"The first card is {card}");

        if (match.PlayerHands.TryGetValue(playerId, out var hand))
        {
            hand.Add(card);
        }

        Console.WriteLine($"card drawn {card}");

        // var responseData = new DataInfo { CardId = card };
        responseData.CardId = card;

        return responseData;
    }

    public DataInfo NextTurn(string matchId, string playerId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return new DataInfo { Error = $"Cannot find match {matchId}" };
        }

        match.NTurns--;

        // Attack card can cause NTurns > 1
        if (match.NTurns <= 0)
        {
            // Advance the turn to the next player if current has none
            int currentIndex = match.PlayerIds.IndexOf(playerId);
            int nextIndex = (currentIndex + 1) % match.PlayerIds.Count;
            match.CurrentTurnPlayerId = match.PlayerIds[nextIndex];

            // Set NTrns to 1
            match.NTurns = 1;
        }

        var responseData = new DataInfo
        {
            NextPlayer = match.CurrentTurnPlayerId,
            Turns = match.NTurns
        };

        return responseData;
    }

    //Safely gets a single player's hand without exposing the whole GameState
    public List<string> GetPlayerHand(string matchId, string playerId)
    {
        if (_activeMatches.TryGetValue(matchId, out var match) && match.PlayerHands.TryGetValue(playerId, out var hand))
        {
            return hand;
        }
        return new List<string>();
    }

    public DataInfo CheckCardLimit(string matchId, string playerId, string newLimit)
    {
        // hands.TryGetValue(playerId, out var hand);
        // var count = hand.Count;
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return new DataInfo { Error = $"Cannot find match {matchId}" };
        }
        if (!match.PlayerHands.TryGetValue(playerId, out var hand))
        {
            Console.WriteLine($"Cannot find player {playerId}");
            return new DataInfo { Error = $"Cannot find player {playerId}" };
        }

        foreach (string Id in match.PlayerIds)
        {
            Console.WriteLine($"{Id}");
        }

        var responseData = new DataInfo { };

        // if card count is less then the limit return
        if (hand.Count <= match.CardLimit)
        {
            responseData.Message = "Good";
        }
        else
        {
            // remove from cycle
            match.PlayerIds.Remove(playerId);
            // remove hand from dict
            match.PlayerHands.Remove(playerId);
            responseData.Message = " Removed";
        }
        if (newLimit == "1")
        {
            match.CardLimit--;
        }
        return responseData;
    }
}
