namespace Aegis.Infrastructure.Data.Entities;

public sealed class RssFeedEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastFetchedAt { get; set; }
    public string? DefaultRegionQuery { get; set; }

    public ICollection<NewsItemEntity> NewsItems { get; set; } = [];
}

public sealed class NewsItemEntity
{
    public Guid Id { get; set; }
    public Guid FeedId { get; set; }
    public RssFeedEntity Feed { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ImageUrl { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public string LinkHash { get; set; } = string.Empty;
}
