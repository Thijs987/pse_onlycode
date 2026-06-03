namespace Domain;

public class AppUser
{
    public Guid Id { get; set; }

    public string Email { get; set; } = default!;
    public string Username { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    // account lockout & security
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }

    // email verification
    public bool IsEmailVerified { get; set; } = false;
    public string? VerificationToken { get; set; }
    public DateTime? VerificationTokenExpiry { get; set; }

    // stats
    public int Wins { get; set; }
    public int Losses { get; set; }

    // current lobby (NULL = geen lobby)
    public Guid? CurrentLobbyId { get; set; }
    public Lobby? CurrentLobby { get; set; }
}