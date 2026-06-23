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

    // password reset (single-use hashed token)
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

    // soft delete -> we keep them but anonymize them and prevent login (so we don't lose game history, but they can't log in anymore)
    public Boolean IsDeleted { get; set; } = false;

    // stats
    public int Wins { get; set; }
    public int Losses { get; set; }

    // Refresh tokens for this user (for refresh/rotation)
    public ICollection<RefreshToken>? RefreshTokens { get; set; }
}