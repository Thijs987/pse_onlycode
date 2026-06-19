using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        // table name
        builder.ToTable("Users");

        // primary key
        builder.HasKey(x => x.Id);

        // required fields
        builder.Property(x => x.Email).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PasswordHash).IsRequired();

        // unique constraints -> prevents race conditions (on db level)
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique();

        // email verification
        builder.Property(x => x.IsEmailVerified).HasDefaultValue(false);
        builder.Property(x => x.VerificationToken).HasMaxLength(500);
        // password reset
        builder.Property(x => x.PasswordResetToken).HasMaxLength(500);
        builder.Property(x => x.PasswordResetTokenExpiry);

        // account lockout
        builder.Property(x => x.FailedLoginAttempts).HasDefaultValue(0);
        builder.Property(x => x.LockoutEnd);

        // soft delete
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        // stats
        builder.Property(x => x.Wins).HasDefaultValue(0);
        builder.Property(x => x.Losses).HasDefaultValue(0);

        // A user can be in a lobby or not, but if they are in a lobby, we want to know which one
        builder.HasOne(x => x.CurrentLobby)
            .WithMany(l => l.Players)
            .HasForeignKey(x => x.CurrentLobbyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}