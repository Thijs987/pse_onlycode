/*
    The MatchManager handles the server-side state of the matches.
    It does NOT send any form of messages to the player(s).
    This is done by the ConnectionManager and called by the MessageRouter.
*/
using System.Collections.Concurrent;
using Domain;
using Application.Interfaces;
using System.Text.RegularExpressions;

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

    public GameState StartNewMatch(string matchId, List<string> players)
    {
        var newState = new GameState
        {
            MatchId = matchId,
            PlayerIds = players,
            CurrentTurnPlayerId = players[0]
        };

        foreach (var player in players) {
            newState.PlayerStatuses[player] = PlayerStatus.Active;
        }

        var allCards = new Dictionary<string, int>()
        {
            {"blue", 0},
            {"cm", 20},
            {"ddos", 20},
            {"err", 0},
            {"garb", 0},
            {"goto", 0},
            {"imp", 20},
            {"inf", 0},
            {"merge", 0},
            {"miracle", 0},
            {"nocom", 0},
            {"sql", 0},
            {"trojan", 0},
            {"vibe", 0},
            {"test", 0}
        };

        foreach (var card in allCards)
        {
            for (int i = 0; i < card.Value; i++)
            {
                newState.TableCards.Add(card.Key);
            }
        }

        GenerateDeck(newState);

        int size = newState.Deck.Count;
        int impcards = newState.Deck.Count(card => card == "imp");


        if ((size-impcards) < (players.Count*3)) {
            Console.WriteLine($"Match {matchId} initialization failed!");
            newState.Deck = new List<string>{};
            return newState;
        }

        // Initialize hands and deal 3 cards per player
        foreach (var player in players)
        {
            newState.PlayerHands[player] = new List<string>();

            for (int i = 0; i < initialHandSize; i++)
            {
                if (newState.Deck.Count < 0)
                {
                    continue;
                }
                string card = newState.Deck[0];
                newState.Deck.RemoveAt(0);
                if (card != "imp") {
                    newState.PlayerHands[player].Add(card);
                } else {
                    newState.TableCards.Add(card);
                    i--;
                }
            }
        }
        var rand = new Random();
        newState.Deck = newState.Deck.OrderBy(_ => rand.Next()).ToList();

        _activeMatches.TryAdd(matchId, newState);
        Console.WriteLine($"Match {matchId} started! Handed out initial hands HAHAHAHAHA.");

        // return new DataInfo { NextPlayer = newState.CurrentTurnPlayerId };
        return newState;
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

        if (GetActive(match).Count <= 1)
            return new DataInfo { Error = "The game has already ended." };

        if (match.CurrentTurnPlayerId != playerId)
        {
            Console.WriteLine($"{playerId} tried to play, but not their turn!");
            return new DataInfo { Error = "Not your turn" };
        }

        if (!match.PlayerHands.TryGetValue(playerId, out var hands))
        {
            return new DataInfo { Error = "You have been eliminated or are not in the game." };
        }

        // Look up the card in registry
        if (!_cardRegistry.TryGetValue(cardData.CardId, out var cardEffect))
        {
            return new DataInfo { Error = $"Unknown card: {cardData.CardId}" };
        }

        if (!string.IsNullOrEmpty(match.PendingAction) && match.PendingActionPlayerId == playerId)
        {
            if (cardData.CardId != match.PendingAction)
            {
                return new DataInfo { Error = "You must resolve your current pending action before playing another card." };
            }
        }
        else if (!match.PlayerHands[playerId].Contains(cardData.CardId))
        {
            Console.WriteLine($"{playerId} tried to play a card they don't have: {cardData.CardId}");
            return new DataInfo { Error = "You do not have that card in your hand!" };
        }

        if (!match.PlayerHands[playerId].Contains("imp"))
        {
            // Apply the specific card's logic
            var responseData = cardEffect.ApplyEffect(match, playerId, cardData);
            // Apply the specific card's logic

            // If no errors, add it to the table
            if (string.IsNullOrEmpty(responseData.Error))
            {
                match.TableCards.Add(cardData.CardId);
            }
            return responseData;
        }
        else if (match.PlayerHands[playerId].Count < 2)
        {
            match.TableCards.Add(cardData.CardId);
            match.PlayerHands[playerId].Remove(cardData.CardId);
            var responseData = new DataInfo
            {
                CardId = cardData.CardId
            };
            responseData.Cards.Add(cardData.CardId);

            return responseData;
        }
        else if (cardData.CardId != "imp")
        {
            match.TableCards.Add("imp");
            match.TableCards.Add(cardData.CardId);
            match.PlayerHands[playerId].Remove("imp");
            match.PlayerHands[playerId].Remove(cardData.CardId);

            var responseData = new DataInfo
            {
                CardId = "imp",
            };
            responseData.Cards.Add("imp");
            responseData.Cards.Add(cardData.CardId);

            return responseData;
        }
        else
        {
            var responseData = new DataInfo
            {
                CardId = "imp",
                Error = "illigal play"
            };
            return responseData;
        }

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

        if (GetActive(match).Count <= 1)
            return new DataInfo { Error = "The game has already ended." };

        if (!match.PlayerHands.ContainsKey(playerId))
            return new DataInfo { Error = "You have been eliminated or are not in the game." };

        if (match.CurrentTurnPlayerId != playerId)
        {
            Console.WriteLine($"{playerId} tried to draw, but not their turn!");
            return new DataInfo { Error = $"Not your turn" };
        }

        if (!string.IsNullOrEmpty(match.PendingAction) && match.PendingActionPlayerId == playerId)
        {
            Console.WriteLine($"{playerId} tried to draw, but has a pending action!");
            return new DataInfo { Error = "You must resolve your current pending action before drawing." };
        }

        var responseData = new DataInfo { };

        if (match.PlayerHands[playerId].Contains("imp"))
        {
            responseData.CardId = "imp";
            return responseData;
        }

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

        if (card == "imp")
        {
            responseData.Message = "improved hardware";
        }
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
            var currentCycle = GetActive(match);
            // Advance the turn to the next player if current has none
            int currentIndex = currentCycle. IndexOf(playerId);
            int nextIndex = (currentIndex + 1) % currentCycle.Count;
            match.CurrentTurnPlayerId = currentCycle[nextIndex];

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

        var responseData = new DataInfo { };

        // if card count is less then the limit return
        if (hand.Count <= match.CardLimit)
        {
            responseData.Message = "Good";
        }
        else
        {
            // remove from cycle
            match.PlayerStatuses[playerId] = PlayerStatus.Eliminated;
            // remove hand from dict
            match.PlayerHands.Remove(playerId);
            responseData.Message = "Removed";
        }
        if (newLimit == "1")
        {
            match.CardLimit--;
        }
        return responseData;
    }

    // Get the match from the playerId.
    public DataInfo RemoveFromMatch(string playerId, string matchId = "")
    {
        if (string.IsNullOrEmpty(matchId))
        {
            matchId = GetMatchFromPlayer(playerId);
        }

        // Check if lookup found something
        if (string.IsNullOrEmpty(matchId))
        {
            return new DataInfo();
        }

        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return new DataInfo { Error = $"Cannot find match {matchId}" };
        }

        var currentCycle = GetActive(match);
        // Check who the next player is and change
        if (match.CurrentTurnPlayerId == playerId)
        {
            // Advance the turn to the next player if current has none
            int currentIndex = currentCycle. IndexOf(playerId);
            int nextIndex = (currentIndex + 1) % currentCycle.Count;
            match.CurrentTurnPlayerId = currentCycle[nextIndex];

            // Set NTrns to 1
            match.NTurns = 1;
        }

        // remove from cycle
        match.PlayerStatuses[playerId] = PlayerStatus.Eliminated;
        // remove hand from dict
        foreach (var card in match.PlayerHands[playerId]) {
            match.TableCards.Add(card);
        }
        match.PlayerHands.Remove(playerId);

        Console.WriteLine($"Removed {playerId} from {matchId}. Cycle: {currentCycle.Count}");

        var responseData = new DataInfo
        {
            NextPlayer = match.CurrentTurnPlayerId,
            Turns = match.NTurns
        };
        return responseData;
    }

    public DataInfo Disconnect(string playerId, string matchId = "") {
        if (string.IsNullOrEmpty(matchId))
        {
            matchId = GetMatchFromPlayer(playerId);
        }

        // Check if lookup found something
        if (string.IsNullOrEmpty(matchId))
        {
            return new DataInfo();
        }

        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return new DataInfo { Error = $"Cannot find match {matchId}" };
        }
        if(match.PlayerStatuses[playerId] == PlayerStatus.Eliminated) {
            match.PlayerStatuses[playerId] = PlayerStatus.DisconnectedEliminated;
        } else if(match.PlayerStatuses[playerId] == PlayerStatus.Active) {
            match.PlayerStatuses[playerId] = PlayerStatus.DisconnectedActive;
        }
        var responseData = new DataInfo
        {
            NextPlayer = match.CurrentTurnPlayerId,
            Turns = match.NTurns
        };
        return responseData;
    }

    public bool Rejoin (string playerId) {
        var matchId = GetMatchFromPlayer(playerId);

        // Check if lookup found something
        if (string.IsNullOrEmpty(matchId))
        {
            return false;
        }
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            Console.WriteLine($"Cannot find match {matchId}");
            return false;
        }

        if (match.PlayerStatuses.TryGetValue(playerId, out var status)) {
            if (match.PlayerStatuses[playerId] == PlayerStatus.DisconnectedEliminated) {
                match.PlayerStatuses[playerId] = PlayerStatus.Eliminated;
                return true;
            } else if (match.PlayerStatuses[playerId] == PlayerStatus.DisconnectedActive) {
                match.PlayerStatuses[playerId] = PlayerStatus.Active;
                return true;
            }
        }
        return false;
    }

    public string GetMatchFromPlayer(string playerId)
    {
        string match = _activeMatches.FirstOrDefault(m => m.Value.PlayerIds.Contains(playerId)).Key;
        return match;
    }

    public bool HasMatchStarted(string matchId)
    {
        return _activeMatches.ContainsKey(matchId);
    }

    public string GetWinner(string matchId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            return string.Empty;
        }

        var activePlayers = GetActive(match);

        if (activePlayers.Count == 1) {
            return activePlayers[0];
        } else {
            return string.Empty;
        }
    }

    public List<string> GetActive(GameState match) {
        var activePlayers = match.PlayerIds
            .Where (id =>
                match.PlayerStatuses.TryGetValue(id, out var status) &&
                status == PlayerStatus.Active || status == PlayerStatus.DisconnectedActive).ToList();
        return activePlayers;
    }

    public List<string> GetActives(string matchId) {
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            return new List<string> {};
        }
        var activePlayers = match.PlayerIds
            .Where (id =>
                match.PlayerStatuses.TryGetValue(id, out var status) &&
                status == PlayerStatus.Active).ToList();
        return activePlayers;
    }

    public int GetDeckSize(string matchId) {
        if (!_activeMatches.TryGetValue(matchId, out var match))
        {
            return -1;
        }
        return match.Deck.Count;
    }
}
