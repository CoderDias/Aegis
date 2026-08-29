using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Intel;

namespace Aegis.Application.Services;

public sealed class RssFeedService(IRssFeedStore store)
{
    public Task<IReadOnlyList<RssFeedDto>> ListFeedsAsync(CancellationToken cancellationToken = default) =>
        store.ListAsync(cancellationToken);

    public Task<RssFeedDto> AddFeedAsync(CreateRssFeedRequest request, CancellationToken cancellationToken = default) =>
        store.CreateAsync(request, cancellationToken);

    public Task<RssFeedDto> UpdateFeedAsync(
        Guid id,
        UpdateRssFeedRequest request,
        CancellationToken cancellationToken = default) =>
        store.UpdateAsync(id, request, cancellationToken);

    public Task DeleteFeedAsync(Guid id, CancellationToken cancellationToken = default) =>
        store.DeleteAsync(id, cancellationToken);

    public Task SetFeedEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default) =>
        store.SetFeedEnabledAsync(id, enabled, cancellationToken);

    public Task<IReadOnlyList<NewsItemDto>> ListNewsAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        store.ListNewsAsync(limit, cancellationToken);
}
