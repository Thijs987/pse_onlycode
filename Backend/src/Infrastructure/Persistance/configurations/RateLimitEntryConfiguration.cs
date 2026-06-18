using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RateLimitEntryConfiguration : IEntityTypeConfiguration<RateLimitEntry>
{
    public void Configure(EntityTypeBuilder<RateLimitEntry> builder)
    {
        builder.ToTable("RateLimitEntries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AttemptedAt).IsRequired();

        builder.HasIndex(x => new { x.Key, x.AttemptedAt });
    }
}
