using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Osint;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.OsintBrazuca;

public sealed class OsintLinkHealthService(
    IOsintBrazucaCatalog catalog,
    OsintBlockedUrlStore blockedStore,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<OsintLinkHealthService> logger) : IOsintLinkHealthService
{
    private const string BrokenLinksCacheKey = "osint:broken-links:v1";
    private static readonly TimeSpan EntryTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan BrokenListTtl = TimeSpan.FromHours(6);

    public async Task<OsintLinkHealthStatus?> GetStatusAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var normalized = NormalizeUrl(url);
        var cacheKey = CacheKey(normalized);
        if (cache.TryGetValue<OsintLinkHealthStatus>(cacheKey, out var cached))
        {
            return cached;
        }

        var status = await ProbeAsync(normalized, cancellationToken).ConfigureAwait(false);
        cache.Set(cacheKey, status, EntryTtl);
        return status;
    }

    public async Task<IReadOnlyDictionary<string, OsintLinkHealthStatus>> GetStatusesAsync(
        IEnumerable<string> urls,
        CancellationToken cancellationToken = default)
    {
        var distinct = urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(NormalizeUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new Dictionary<string, OsintLinkHealthStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in distinct)
        {
            var status = await GetStatusAsync(url, cancellationToken).ConfigureAwait(false);
            if (status is not null)
            {
                result[url] = status;
            }
        }

        return result;
    }

    public Task<IReadOnlyList<OsintBrokenLinkReport>> GetBrokenLinksAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<IReadOnlyList<OsintBrokenLinkReport>>(BrokenLinksCacheKey, out var cached) && cached is not null)
        {
            return Task.FromResult(cached);
        }

        return Task.FromResult<IReadOnlyList<OsintBrokenLinkReport>>([]);
    }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        var sources = catalog.GetAllSources();
        var broken = new List<OsintBrokenLinkReport>();
        var batch = sources.Take(120).ToList();
        foreach (var source in batch)
        {
            var status = await GetStatusAsync(source.Url, cancellationToken).ConfigureAwait(false);
            if (status is { IsOnline: false })
            {
                broken.Add(new OsintBrokenLinkReport(
                    source.FonteId,
                    source.Fonte,
                    source.Url,
                    status.StatusCode,
                    status.Error,
                    status.CheckedAt));
            }
        }

        cache.Set(BrokenLinksCacheKey, broken, BrokenListTtl);
        logger.LogInformation("Health-check OSINT Brazuca: {Broken}/{Total} links offline.", broken.Count, batch.Count);
    }

    private async Task<OsintLinkHealthStatus> ProbeAsync(string url, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("OsintHealth");

        foreach (var method in new[] { HttpMethod.Head, HttpMethod.Get })
        {
            try
            {
                using var request = new HttpRequestMessage(method, url);
                using var response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                var code = (int)response.StatusCode;

                if (code == 404 && method == HttpMethod.Head)
                {
                    continue;
                }

                if (code == 404)
                {
                    blockedStore.Block(url, 404);
                }

                var online = code is >= 200 and < 400;
                return new OsintLinkHealthStatus(url, online, code, null, DateTimeOffset.UtcNow);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new OsintLinkHealthStatus(url, false, null, "timeout", DateTimeOffset.UtcNow);
            }
            catch (HttpRequestException ex)
            {
                if (method == HttpMethod.Get)
                {
                    return new OsintLinkHealthStatus(url, false, null, ex.Message, DateTimeOffset.UtcNow);
                }
            }
        }

        return new OsintLinkHealthStatus(url, false, null, "unreachable", DateTimeOffset.UtcNow);
    }

    private static string NormalizeUrl(string url) => url.Trim();

    private static string CacheKey(string url) => $"osint:health:{url.ToLowerInvariant()}";
}
