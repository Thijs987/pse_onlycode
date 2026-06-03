public class GameState
{
    // Add = string.Empty; to suppress the warnings
    public string MatchId { get; set; } = string.Empty;
    public List<string> PlayerIds { get; set; } = new();
    public string CurrentTurnPlayerId { get; set; } = string.Empty;

    public Dictionary<string, List<string>> PlayerHands { get; set; } = new();
    public List<string> TableCards { get; set; } = new();
}