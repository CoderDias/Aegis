using Aegis.Infrastructure.Data.Converters;
using Aegis.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class DiscoveredHostEntityConfiguration : IEntityTypeConfiguration<DiscoveredHostEntity>
{
    public void Configure(EntityTypeBuilder<DiscoveredHostEntity> builder)
    {
        builder.ToTable("DiscoveredHosts");
        builder.HasKey(x => x.Ip);
        builder.Property(x => x.Ip).HasMaxLength(45).IsRequired();
        builder.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.City).HasMaxLength(128);
        builder.Property(x => x.Country).HasMaxLength(128);
        builder.Property(x => x.Org).HasMaxLength(256);
        builder.Property(x => x.Product).HasMaxLength(128);
        builder.Property(x => x.Transport).HasMaxLength(16);
        builder.Property(x => x.Source).HasMaxLength(32).IsRequired();
        builder.Property(x => x.LastProbeAt).HasConversion(new NullableDateTimeOffsetConverter());
        builder.Property(x => x.CensysFetchedAt).HasConversion(new NullableDateTimeOffsetConverter());
        builder.Property(x => x.CreatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.Property(x => x.UpdatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.HasIndex(x => new { x.CountryCode, x.Lat, x.Lng });
        builder.HasIndex(x => x.CensysFetchedAt);
    }
}

public sealed class CensysApiUsageEntityConfiguration : IEntityTypeConfiguration<CensysApiUsageEntity>
{
    public void Configure(EntityTypeBuilder<CensysApiUsageEntity> builder)
    {
        builder.ToTable("CensysApiUsage");
        builder.HasKey(x => x.MonthKey);
        builder.Property(x => x.MonthKey).HasMaxLength(7);
    }
}

public sealed class CountryIngestStateEntityConfiguration : IEntityTypeConfiguration<CountryIngestStateEntity>
{
    public void Configure(EntityTypeBuilder<CountryIngestStateEntity> builder)
    {
        builder.ToTable("CountryIngestStates");
        builder.HasKey(x => x.CountryCode);
        builder.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.SearchPageToken).HasMaxLength(512);
        builder.Property(x => x.UpdatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.Property(x => x.LastPrefetchUtc).HasConversion(new NullableDateTimeOffsetConverter());
    }
}
