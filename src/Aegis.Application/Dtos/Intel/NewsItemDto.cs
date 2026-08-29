namespace Aegis.Application.Dtos.Intel;

public record NewsItemDto(
    Guid Id,
    Guid FeedId,
    string FeedTitle,
    string Title,
    string Link,
    string? Summary,
    DateTimeOffset PublishedAt,
    double? Lat,
    double? Lng,
    string? ImageUrl = null);

public record RssFeedDto(
    Guid Id,
    string Title,
    string Url,
    bool Enabled,
    DateTimeOffset? LastFetchedAt,
    string? DefaultRegionQuery = null,
    int TotalItems = 0,
    int GeolocatedItems = 0);

public record CreateRssFeedRequest(string Title, string Url, string? RegionQuery = null);

public record UpdateRssFeedRequest(string Title, string Url, string? RegionQuery = null);
