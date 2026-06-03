namespace Domain;

public class Lobby
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public Guid HostUserId { get; set; }
    public AppUser HostUser { get; set; } = default!;

    public LobbyStatus Status { get; set; } = LobbyStatus.Waiting;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 1 lobby -> veel users
    public ICollection<AppUser> Players { get; set; } = new List<AppUser>();
}