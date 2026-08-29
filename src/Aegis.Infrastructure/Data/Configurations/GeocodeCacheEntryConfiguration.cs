using Aegis.Infrastructure.Data.Converters;
using Aegis.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class GeocodeCacheEntryConfiguration : IEntityTypeConfiguration<GeocodeCacheEntry>
{
    public void Configure(EntityTypeBuilder<GeocodeCacheEntry> builder)
    {
        builder.ToTable("GeocodeCache");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.QueryHash).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Kind).IsRequired();
        builder.Property(e => e.RequestJson).IsRequired();
        builder.Property(e => e.ResponseJson).IsRequired();
        builder.Property(e => e.CreatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.Property(e => e.ExpiresAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();

        builder.HasIndex(e => e.QueryHash).IsUnique();
        builder.HasIndex(e => e.ExpiresAt);
    }
}
