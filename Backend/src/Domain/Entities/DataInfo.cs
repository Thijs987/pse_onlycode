using System.Text.Json.Serialization;

public class DataInfo
{
    // which card is it?
    [JsonPropertyName("cardId")]
    public string? CardId { get; set; }

    // who should the card target?
    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    // Next player
    [JsonPropertyName("nextPlayer")]
    public string? NextPlayer { get; set; }

    //Amount of moves for next player
    [JsonPropertyName("turns")]
    public int? Turns { get; set; } = 1;

    //Error message
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    // Used for sending hands or multiple cards
    [JsonPropertyName("cards")]
    public List<string>? Cards { get; set; } = new();

    // Determines if this message should only be sent to the specific player
    [JsonPropertyName("isPrivate")]
    public bool? IsPrivate { get; set; } = false;

    // Number of cards remaining in the draw pile (sent on match start)
    [JsonPropertyName("deckSize")]
    public int? DeckSize { get; set; }

    // True when this draw exhausted the draw pile and reshuffled it from the discard pile
    [JsonPropertyName("deckRefilled")]
    public bool? DeckRefilled { get; set; }

    // Current hand-size limit for the match (starts at 5, drops by 1 each time the pile empties)
    [JsonPropertyName("cardLimit")]
    public int? CardLimit { get; set; }

    [JsonPropertyName("players")]
    public List<string>? Players { get; set; } = new();
}
