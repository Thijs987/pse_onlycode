using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AppUser?> Login(string email, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);

        if (user == null) return null;
        if (!PasswordHasher.Verify(password, user.PasswordHash)) return null;

        return user;
    }

    public async Task<AppUser> Register(string email, string username, string password)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = PasswordHasher.Hash(password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return user;
    }
}