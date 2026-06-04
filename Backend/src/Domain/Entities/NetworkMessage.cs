using System.Text.Json.Serialization;

public class NetworkMessage
{
    // What are we doing? example: "JOIN_LOBBY"
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    // Who sent this?
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    // The actual data, example: "cardPlayed": "Garbage Collector"
    [JsonPropertyName("data")]
    public DataInfo Data { get; set; } = new();
}
