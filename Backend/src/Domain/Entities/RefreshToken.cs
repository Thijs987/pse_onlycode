using System;

namespace Domain;

public class RefreshToken
{
    public Guid Id { get; set; }

    // Hashed token value (use PasswordHasher.Hash to store)
    public string TokenHash { get; set; } = default!;

    public DateTime Expires { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Revoked { get; set; }

    // Optional metadata
    public string? CreatedByIp { get; set; }
    public string? ReplacedByToken { get; set; }

    // relationship
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
}
