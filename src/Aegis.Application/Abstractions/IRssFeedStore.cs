using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;

namespace Aegis.Application.Abstractions;

public interface IRssFeedStore
{
    Task<IReadOnlyList<RssFeedDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<RssFeedDto> CreateAsync(CreateRssFeedRequest request, CancellationToken cancellationToken = default);
    Task<RssFeedDto> UpdateAsync(Guid id, UpdateRssFeedRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetFeedEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);
    Task UpdateLastFetchedAsync(Guid id, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default);
    Task UpsertNewsItemsAsync(Guid feedId, IReadOnlyList<NewsItemDto> items, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsItemDto>> ListNewsAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsItemDto>> ListGeolocatedNewsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsItemDto>> ListNewsInViewportAsync(BoundingBoxDto bbox, int limit, CancellationToken cancellationToken = default);
    void InvalidateNewsCache();
}
