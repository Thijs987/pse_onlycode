public class GameState
{
    // Add = string.Empty; to suppress the warnings
    public string MatchId { get; set; } = string.Empty;
    public List<string> PlayerIds { get; set; } = new();
    public string CurrentTurnPlayerId { get; set; } = string.Empty;
    public int NTurns { get; set; } = 1;
    public int CardLimit { get; set; } = 5;
    // Cards players have in their hand
    public Dictionary<string, List<string>> PlayerHands { get; set; } = new();
    // Played cards
    public List<string> TableCards { get; set; } = new();
    // Deck on table
    public LinkedList<string> Deck { get; set; } = new();
}
