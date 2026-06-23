namespace Domain;

public class GameMatch
{
    public Guid Id { get; set; }

    public Guid LobbyId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }

    public Guid WinnerUserId { get; set; }
    public AppUser WinnerUser { get; set; } = default!;
}