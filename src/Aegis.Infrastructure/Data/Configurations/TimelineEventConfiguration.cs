using Aegis.Domain.Entities;
using Aegis.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class TimelineEventConfiguration : IEntityTypeConfiguration<TimelineEvent>
{
    public void Configure(EntityTypeBuilder<TimelineEvent> builder)
    {
        builder.ToTable("TimelineEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnType("TEXT");
        builder.Property(e => e.InvestigationId).HasColumnType("TEXT").IsRequired();
        builder.Property(e => e.OccurredAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.Property(e => e.Type).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.PayloadJson);
        builder.Property(e => e.IsRead).HasDefaultValue(false);

        builder.HasIndex(e => new { e.InvestigationId, e.OccurredAt });
    }
}
