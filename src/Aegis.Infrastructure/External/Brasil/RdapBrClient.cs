using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Osint;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.Brasil;

public sealed class RdapBrClient(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<RdapBrClient> logger) : IRdapBrClient
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public async Task<RdapDomainDto?> GetDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDomain(domain);
        if (normalized is null)
        {
            return null;
        }

        if (cache.TryGetValue<RdapDomainDto>(CacheKey(normalized), out var cached))
        {
            return cached;
        }

        try
        {
            var client = httpClientFactory.CreateClient("RdapBr");
            using var response = await client.GetAsync($"domain/{normalized}", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("RDAP BR {Domain} retornou {Status}", normalized, response.StatusCode);
                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<RdapResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (payload is null)
            {
                return null;
            }

            var dto = new RdapDomainDto(
                normalized,
                payload.Status?.FirstOrDefault(),
                payload.Nameservers?
                    .Select(n => n.LdhnName ?? n.Ldhn ?? string.Empty)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList() ?? [],
                ParseDate(payload.Events?.FirstOrDefault(e =>
                    e.EventAction?.Contains("registration", StringComparison.OrdinalIgnoreCase) == true)?.EventDate),
                ParseDate(payload.Events?.FirstOrDefault(e =>
                    e.EventAction?.Contains("expiration", StringComparison.OrdinalIgnoreCase) == true)?.EventDate),
                payload.Entities?.FirstOrDefault()?.Handle);

            cache.Set(CacheKey(normalized), dto, CacheDuration);
            return dto;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Falha ao consultar RDAP para {Domain}.", normalized);
            return null;
        }
    }

    private static string? NormalizeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        var value = domain.Trim().ToLowerInvariant();
        if (value.StartsWith("http://", StringComparison.Ordinal))
        {
            value = value["http://".Length..];
        }
        else if (value.StartsWith("https://", StringComparison.Ordinal))
        {
            value = value["https://".Length..];
        }

        var slash = value.IndexOf('/');
        if (slash >= 0)
        {
            value = value[..slash];
        }

        return value.Contains('.') ? value : null;
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private static string CacheKey(string domain) => $"rdap-br:{domain}";

    private sealed class RdapResponse
    {
        public List<string>? Status { get; set; }
        public List<RdapEvent>? Events { get; set; }
        public List<RdapEntity>? Entities { get; set; }
        public List<RdapNameserver>? Nameservers { get; set; }
    }

    private sealed class RdapEvent
    {
        [JsonPropertyName("eventAction")]
        public string? EventAction { get; set; }

        [JsonPropertyName("eventDate")]
        public string? EventDate { get; set; }
    }

    private sealed class RdapEntity
    {
        public string? Handle { get; set; }
    }

    private sealed class RdapNameserver
    {
        [JsonPropertyName("ldhName")]
        public string? LdhnName { get; set; }

        public string? Ldhn { get; set; }
    }
}
