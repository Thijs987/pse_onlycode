using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class LobbyConfiguration : IEntityTypeConfiguration<Lobby>
{
    public void Configure(EntityTypeBuilder<Lobby> builder)
    {
        // table name
        builder.ToTable("Lobbies");

        // primary key
        builder.HasKey(x => x.Id);

        // required fields
        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(6);

        // unique code
        builder.HasIndex(x => x.Code).IsUnique();

        // host user
        builder.HasOne(x => x.HostUser)
            .WithMany()
            .HasForeignKey(x => x.HostUserId);
    }
}