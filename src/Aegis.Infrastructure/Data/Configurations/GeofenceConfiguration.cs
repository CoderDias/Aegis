using Aegis.Domain.Entities;
using Aegis.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class GeofenceConfiguration : IEntityTypeConfiguration<Geofence>
{
    public void Configure(EntityTypeBuilder<Geofence> builder)
    {
        builder.ToTable("Geofences");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnType("TEXT");
        builder.Property(g => g.InvestigationId).HasColumnType("TEXT").IsRequired();
        builder.Property(g => g.Name).HasMaxLength(120).IsRequired();
        builder.Property(g => g.GeometryJson).IsRequired();
        builder.Property(g => g.IsEnabled).IsRequired();
        builder.Property(g => g.CreatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();

        builder.HasIndex(g => g.InvestigationId);
    }
}
