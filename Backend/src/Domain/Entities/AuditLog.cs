namespace Domain;

public class AuditLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
}
