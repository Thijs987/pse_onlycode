using System.Text.Json.Serialization;

public class DataInfo
{
    //which card is it?
    [JsonPropertyName("cardId")]
    public string CardId { get; set; } = string.Empty;

    //who should the card target?
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    //Next player
    [JsonPropertyName("nextPlayer")]
    public string NextPlayer { get; set; } = string.Empty;

    //Amount of moves for next player
    [JsonPropertyName("Turns")]
    public int Turns { get; set; } = 1;

    //Error message
    [JsonPropertyName("Error")]
    public string Error { get; set; } = string.Empty;
}
