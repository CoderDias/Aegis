using Aegis.Infrastructure.Data.Converters;
using Aegis.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aegis.Infrastructure.Data.Configurations;

public sealed class RssFeedEntityConfiguration : IEntityTypeConfiguration<RssFeedEntity>
{
    public void Configure(EntityTypeBuilder<RssFeedEntity> builder)
    {
        builder.ToTable("RssFeeds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.DefaultRegionQuery).HasMaxLength(200);
        builder.Property(x => x.CreatedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.Property(x => x.LastFetchedAt).HasConversion(new NullableDateTimeOffsetConverter());
        builder.HasIndex(x => x.Url).IsUnique();
    }
}

public sealed class NewsItemEntityConfiguration : IEntityTypeConfiguration<NewsItemEntity>
{
    public void Configure(EntityTypeBuilder<NewsItemEntity> builder)
    {
        builder.ToTable("NewsItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Link).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.LinkHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(2000);
        builder.Property(x => x.PublishedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.Property(x => x.FetchedAt).HasConversion(new DateTimeOffsetConverter()).IsRequired();
        builder.HasIndex(x => x.LinkHash).IsUnique();
        builder.HasIndex(x => x.PublishedAt);
        builder.HasIndex(x => new { x.Latitude, x.Longitude });

        builder.HasOne(x => x.Feed)
            .WithMany(x => x.NewsItems)
            .HasForeignKey(x => x.FeedId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
