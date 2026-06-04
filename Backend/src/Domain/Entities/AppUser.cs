namespace Domain;

public class AppUser
{
    public Guid Id { get; set; }

    public string Email { get; set; } = default!;
    public string Username { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    // stats
    public int Wins { get; set; }
    public int Losses { get; set; }

    // current lobby (NULL = geen lobby)
    public Guid? CurrentLobbyId { get; set; }
    public Lobby? CurrentLobby { get; set; }
}