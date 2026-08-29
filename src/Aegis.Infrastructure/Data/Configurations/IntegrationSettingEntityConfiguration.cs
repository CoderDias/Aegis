using Aegis.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class IntegrationSettingEntityConfiguration : IEntityTypeConfiguration<IntegrationSettingEntity>
{
    public void Configure(EntityTypeBuilder<IntegrationSettingEntity> builder)
    {
        builder.ToTable("IntegrationSettings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(64);
        builder.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
    }
}
