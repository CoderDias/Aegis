using System.Text.Json;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Infrastructure.Data;
using Aegis.Infrastructure.Data.Entities;
using Aegis.Infrastructure.Geo;
using Aegis.Infrastructure.External.Nominatim;
using Aegis.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Cache;

public sealed class CachedGeocodingService : IGeocodingService
{
    private readonly NominatimClient _nominatim;
    private readonly AegisDbContext _db;
    private readonly IMemoryCache _memoryCache;
    private readonly IbgeMunicipalityCatalog _ibgeCatalog;
    private readonly CacheOptions _cacheOptions;
    private readonly NominatimOptions _nominatimOptions;

    public CachedGeocodingService(
        NominatimClient nominatim,
        AegisDbContext db,
        IMemoryCache memoryCache,
        IbgeMunicipalityCatalog ibgeCatalog,
        IOptions<CacheOptions> cacheOptions,
        IOptions<NominatimOptions> nominatimOptions)
    {
        _nominatim = nominatim;
        _db = db;
        _memoryCache = memoryCache;
        _ibgeCatalog = ibgeCatalog;
        _cacheOptions = cacheOptions.Value;
        _nominatimOptions = nominatimOptions.Value;
    }

    public async Task<IReadOnlyList<GeocodeResultDto>> SearchAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var normalized = NominatimClient.NormalizeQuery(query);
        if (normalized.Length < _nominatimOptions.MinSearchLength)
        {
            return [];
        }

        var hash = NominatimClient.ComputeForwardHash(normalized);
        var memoryKey = $"geocode:forward:{hash}";

        if (_memoryCache.TryGetValue(memoryKey, out IReadOnlyList<GeocodeResultDto>? cached) && cached is not null)
        {
            return cached;
        }

        var dbEntry = await _db.GeocodeCache
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.QueryHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (dbEntry is not null && dbEntry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            var fromDb = DeserializeResults(dbEntry.ResponseJson);
            SetMemoryCache(memoryKey, fromDb);
            return fromDb;
        }

        var results = await _nominatim.SearchAsync(normalized, limit, cancellationToken).ConfigureAwait(false);
        await UpsertCacheAsync(
            hash,
            GeocodeCacheKind.Forward,
            JsonSerializer.Serialize(new { query = normalized, limit }),
            JsonSerializer.Serialize(results),
            cancellationToken).ConfigureAwait(false);

        SetMemoryCache(memoryKey, results);
        return results;
    }

    public async Task<GeocodeResultDto?> ReverseAsync(
        CoordinateDto coordinate,
        CancellationToken cancellationToken = default)
    {
        var hash = NominatimClient.ComputeReverseHash(coordinate);
        var memoryKey = $"geocode:reverse:{hash}";

        if (_memoryCache.TryGetValue(memoryKey, out GeocodeResultDto? cached))
        {
            return cached;
        }

        var dbEntry = await _db.GeocodeCache
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.QueryHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (dbEntry is not null && dbEntry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            var fromDb = DeserializeResult(dbEntry.ResponseJson);
            fromDb = await EnrichBrazilReverseAsync(fromDb, coordinate, cancellationToken).ConfigureAwait(false);
            if (fromDb is not null)
            {
                SetMemoryCache(memoryKey, fromDb);
            }

            return fromDb;
        }

        var result = await _nominatim.ReverseAsync(coordinate, cancellationToken).ConfigureAwait(false);
        result = await EnrichBrazilReverseAsync(result, coordinate, cancellationToken).ConfigureAwait(false);
        await UpsertCacheAsync(
            hash,
            GeocodeCacheKind.Reverse,
            JsonSerializer.Serialize(coordinate),
            JsonSerializer.Serialize(result),
            cancellationToken).ConfigureAwait(false);

        if (result is not null)
        {
            SetMemoryCache(memoryKey, result);
        }

        return result;
    }

    private async Task UpsertCacheAsync(
        string hash,
        GeocodeCacheKind kind,
        string requestJson,
        string responseJson,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var ttlDays = Math.Max(_cacheOptions.GeocodeTtlDays, _nominatimOptions.CacheDays);
        var expiresAt = now.AddDays(ttlDays);

        var existing = await _db.GeocodeCache
            .FirstOrDefaultAsync(e => e.QueryHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.GeocodeCache.Add(new GeocodeCacheEntry
            {
                QueryHash = hash,
                Kind = kind,
                RequestJson = requestJson,
                ResponseJson = responseJson,
                CreatedAt = now,
                ExpiresAt = expiresAt
            });
        }
        else
        {
            existing.Kind = kind;
            existing.RequestJson = requestJson;
            existing.ResponseJson = responseJson;
            existing.CreatedAt = now;
            existing.ExpiresAt = expiresAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void SetMemoryCache<T>(string key, T value)
    {
        _memoryCache.Set(key, value, TimeSpan.FromMinutes(Math.Max(_cacheOptions.GeocodeTtlDays, 1) * 60));
    }

    private static IReadOnlyList<GeocodeResultDto> DeserializeResults(string json)
    {
        return JsonSerializer.Deserialize<List<GeocodeResultDto>>(json) ?? [];
    }

    private static GeocodeResultDto? DeserializeResult(string json) =>
        JsonSerializer.Deserialize<GeocodeResultDto>(json);

    private async Task<GeocodeResultDto?> EnrichBrazilReverseAsync(
        GeocodeResultDto? result,
        CoordinateDto coordinate,
        CancellationToken cancellationToken)
    {
        if (result is null || !IsInBrazil(coordinate))
        {
            return result;
        }

        var parts = result.AddressParts ?? new Dictionary<string, string>();
        var city = parts.GetValueOrDefault("city")
            ?? parts.GetValueOrDefault("town")
            ?? parts.GetValueOrDefault("village")
            ?? parts.GetValueOrDefault("municipality");
        var uf = parts.GetValueOrDefault("state");
        var district = parts.GetValueOrDefault("suburb")
            ?? parts.GetValueOrDefault("neighbourhood")
            ?? parts.GetValueOrDefault("quarter");

        var municipality = await _ibgeCatalog.ResolveAsync(city, uf, cancellationToken).ConfigureAwait(false);
        if (municipality is null && parts.TryGetValue("ISO3166-2", out var iso) && iso.Contains('-'))
        {
            var isoUf = iso.Split('-').LastOrDefault();
            municipality = await _ibgeCatalog.ResolveAsync(city, isoUf, cancellationToken).ConfigureAwait(false);
        }

        var resolvedCity = municipality?.Name ?? city;
        var resolvedUf = municipality?.Uf ?? uf;
        var displayName = _ibgeCatalog.FormatBrazilLabel(resolvedCity, resolvedUf, district);

        var enrichedParts = new Dictionary<string, string>(parts, StringComparer.OrdinalIgnoreCase);
        if (municipality is not null)
        {
            enrichedParts["ibge"] = municipality.IbgeCode;
        }

        if (!string.IsNullOrWhiteSpace(resolvedCity))
        {
            enrichedParts["city"] = resolvedCity;
        }

        if (!string.IsNullOrWhiteSpace(resolvedUf))
        {
            enrichedParts["state"] = resolvedUf;
        }

        return result with
        {
            DisplayName = displayName,
            AddressParts = enrichedParts
        };
    }

    private static bool IsInBrazil(CoordinateDto coordinate) =>
        coordinate.Lat is >= -35 and <= 6 &&
        coordinate.Lng is >= -75 and <= -28;
}
