using System.Security.Cryptography;
using System.Text;

namespace Application;

public static class PasswordHasher
{
    // constants for hashing parameters
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    // Stored format for the hash: iterations:salt:hash (all base64 except iterations)
    public static string Hash(string password)
    {
        /* Hashes a password using PBKDF2 with HMAC-SHA256. The resulting hash is returned
           in the format:
           - iterations:salt:hash
         where iterations is an integer, and salt and hash are base64-encoded strings.

         This method generates a random salt for each password and uses a high iteration count
         to make brute-force attacks more difficult.

         Returns:
         - the hashed password string in the specified format.
        */
        // Generate a random salt
        var salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Derive the key using PBKDF2 with HMAC-SHA256
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt,
                                            Iterations, HashAlgorithmName.SHA256, KeySize);

        return $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string stored)
    {
        /* Verifies a password against the stored hash. The stored hash is expected to be in
           the format:
           -   iterations:salt:hash
         where iterations is an integer, and salt and hash are base64-encoded strings.

         Returns:
         - true if the password is correct
         - false if the password is incorrect.
        */
        // Validate the stored format and extract parameters
        if (string.IsNullOrEmpty(stored)) return false;

        var parts = stored.Split(':');
        if (parts.Length != 3) return false;

        // Parse iterations
        if (!int.TryParse(parts[0], out var iterations)) return false;

        // Decode the salt and stored key from base64
        var salt = Convert.FromBase64String(parts[1]);
        var storedKey = Convert.FromBase64String(parts[2]);

        // Derive the key from the provided password using the same parameters
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt,
                                iterations, HashAlgorithmName.SHA256, storedKey.Length);

        return CryptographicOperations.FixedTimeEquals(key, storedKey);
    }
}