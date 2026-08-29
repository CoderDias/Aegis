using Aegis.Domain.Entities;
using Aegis.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class InvestigationConfiguration : IEntityTypeConfiguration<Investigation>
{
    public void Configure(EntityTypeBuilder<Investigation> builder)
    {
        builder.ToTable("Investigations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnType("TEXT");
        builder.Property(i => i.Title).HasMaxLength(120).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(4000);
        builder.Property(i => i.Status).IsRequired();
        builder.Property(i => i.CreatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.Property(i => i.UpdatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.Property(i => i.ClosedAt).HasConversion(new NullableDateTimeOffsetConverter());

        builder.HasMany(i => i.Assets)
            .WithOne()
            .HasForeignKey(a => a.InvestigationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Annotations)
            .WithOne()
            .HasForeignKey(a => a.InvestigationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Timeline)
            .WithOne()
            .HasForeignKey(e => e.InvestigationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Geofences)
            .WithOne()
            .HasForeignKey(g => g.InvestigationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Assets).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.Annotations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.Timeline).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.Geofences).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
