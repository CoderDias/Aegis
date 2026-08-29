using Aegis.Domain.Entities;
using Aegis.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnType("TEXT");
        builder.Property(a => a.InvestigationId).HasColumnType("TEXT").IsRequired();
        builder.Property(a => a.Type).IsRequired();
        builder.Property(a => a.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Source).IsRequired();
        builder.Property(a => a.ExternalKey).HasMaxLength(256);
        builder.Property(a => a.MetadataJson).IsRequired().HasDefaultValue("{}");
        builder.Property(a => a.Notes);
        builder.Property(a => a.CreatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();

        builder.ComplexProperty(a => a.Location, location =>
        {
            location.Property(c => c.Latitude).HasColumnName("Latitude");
            location.Property(c => c.Longitude).HasColumnName("Longitude");
            location.IsRequired(false);
        });

        builder.HasIndex(a => a.InvestigationId);
        builder.HasIndex(a => a.ExternalKey);
        builder.HasIndex(a => new { a.InvestigationId, a.Type, a.ExternalKey })
            .IsUnique()
            .HasFilter("[ExternalKey] IS NOT NULL");
    }
}
