using Aegis.Domain.Entities;
using Aegis.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class FlightTrackPointConfiguration : IEntityTypeConfiguration<FlightTrackPoint>
{
    public void Configure(EntityTypeBuilder<FlightTrackPoint> builder)
    {
        builder.ToTable("FlightTrackPoints");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.Icao24).HasMaxLength(6).IsRequired();
        builder.Property(p => p.Callsign).HasMaxLength(16);
        builder.Property(p => p.Time).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.Property(p => p.Latitude).IsRequired();
        builder.Property(p => p.Longitude).IsRequired();
        builder.Property(p => p.BaroAltitude);
        builder.Property(p => p.GeoAltitude);
        builder.Property(p => p.Velocity);
        builder.Property(p => p.Heading);
        builder.Property(p => p.VerticalRate);
        builder.Property(p => p.OriginCountry);
        builder.Property(p => p.OnGround).IsRequired();
        builder.Property(p => p.Source).IsRequired();

        builder.HasIndex(p => new { p.Icao24, p.Time });
        builder.HasIndex(p => p.Time);
    }
}
