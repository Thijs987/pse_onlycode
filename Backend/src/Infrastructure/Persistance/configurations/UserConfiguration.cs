using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        // primary key
        builder.HasKey(x => x.Id);

        // required fields
        builder.Property(x => x.Email).IsRequired();
        builder.Property(x => x.Username).IsRequired();

        // unique constraints -> prevents race conditions (on db level)
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique();

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