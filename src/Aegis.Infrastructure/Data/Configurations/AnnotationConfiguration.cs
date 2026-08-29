using Aegis.Domain.Entities;
using Aegis.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class AnnotationConfiguration : IEntityTypeConfiguration<Annotation>
{
    public void Configure(EntityTypeBuilder<Annotation> builder)
    {
        builder.ToTable("Annotations");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnType("TEXT");
        builder.Property(a => a.InvestigationId).HasColumnType("TEXT").IsRequired();
        builder.Property(a => a.Kind).IsRequired();
        builder.Property(a => a.Label);
        builder.Property(a => a.Color).HasMaxLength(7).IsRequired();
        builder.Property(a => a.GeometryJson).IsRequired();
        builder.Property(a => a.CreatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();

        builder.HasIndex(a => a.InvestigationId);
    }
}
