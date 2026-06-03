using System.Security.Cryptography;
using System.Text;

namespace Application;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(
            sha.ComputeHash(Encoding.UTF8.GetBytes(password))
        );
    }

    public static bool Verify(string password, string hash)
        => Hash(password) == hash;
}