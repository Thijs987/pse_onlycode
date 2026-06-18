namespace Domain;

public class RateLimitEntry
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public DateTime AttemptedAt { get; set; }
}
