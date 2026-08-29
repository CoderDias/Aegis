namespace Aegis.Application.Abstractions;

public interface IOsintLinkHealthService
{
    Task<OsintLinkHealthStatus?> GetStatusAsync(string url, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, OsintLinkHealthStatus>> GetStatusesAsync(
        IEnumerable<string> urls,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OsintBrokenLinkReport>> GetBrokenLinksAsync(CancellationToken cancellationToken = default);

    Task RefreshCatalogAsync(CancellationToken cancellationToken = default);
}

public sealed record OsintLinkHealthStatus(
    string Url,
    bool IsOnline,
    int? StatusCode,
    string? Error,
    DateTimeOffset CheckedAt);

public sealed record OsintBrokenLinkReport(
    string FonteId,
    string Fonte,
    string Url,
    int? StatusCode,
    string? Error,
    DateTimeOffset CheckedAt);
