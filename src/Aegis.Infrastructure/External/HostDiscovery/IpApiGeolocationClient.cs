using System.Globalization;
using System.Text.Json;
using Aegis.Infrastructure.Resilience;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.HostDiscovery;

public sealed record IpGeoResult(
    string Ip,
    double Lat,
    double Lng,
    string? City,
    string? Country,
    string? Region,
    string? Org,
    string? Isp);

public sealed class IpApiGeolocationClient(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IOptions<HostDiscoveryOptions> options,
    ILogger<IpApiGeolocationClient> logger)
{
    private readonly SemaphoreSlim _rateGate = new(1, 1);
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    public async Task<IpGeoResult?> LookupAsync(string ip, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"hostdiscovery:geo:{ip}";
        if (cache.TryGetValue(cacheKey, out IpGeoResult? cached))
        {
            return cached;
        }

        await _rateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var delay = options.Value.GeolocationDelayMs -
                        (int)(DateTimeOffset.UtcNow - _lastRequest).TotalMilliseconds;
            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            var client = httpClientFactory.CreateClient(HttpClientNames.IpApi);
            var fields = "status,message,country,countryCode,regionName,city,lat,lon,isp,org,query";
            using var response = await client
                .GetAsync($"json/{ip}?fields={fields}", cancellationToken)
                .ConfigureAwait(false);

            _lastRequest = DateTimeOffset.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out var statusProp) &&
                statusProp.GetString() != "success")
            {
                return null;
            }

            if (!root.TryGetProperty("lat", out var latProp) ||
                !root.TryGetProperty("lon", out var lngProp))
            {
                return null;
            }

            var result = new IpGeoResult(
                ip,
                latProp.GetDouble(),
                lngProp.GetDouble(),
                root.TryGetProperty("city", out var cityProp) ? cityProp.GetString() : null,
                root.TryGetProperty("country", out var countryProp) ? countryProp.GetString() : null,
                root.TryGetProperty("regionName", out var regionProp) ? regionProp.GetString() : null,
                root.TryGetProperty("org", out var orgProp) ? orgProp.GetString() : null,
                root.TryGetProperty("isp", out var ispProp) ? ispProp.GetString() : null);

            cache.Set(cacheKey, result, TimeSpan.FromHours(24));
            return result;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "ip-api lookup failed for {Ip}", ip);
            return null;
        }
        finally
        {
            _rateGate.Release();
        }
    }
}

internal static class PortProductMapper
{
    private static readonly Dictionary<int, string> Known = new()
    {
        [80] = "HTTP",
        [443] = "HTTPS",
        [8080] = "HTTP-Alt",
        [8443] = "HTTPS-Alt",
        [554] = "RTSP/CCTV",
        [37777] = "DVR/CCTV",
        [8000] = "HTTP/CCTV",
        [8888] = "HTTP/CCTV",
        [21] = "FTP",
        [22] = "SSH",
        [23] = "Telnet",
        [161] = "SNMP",
        [502] = "Modbus",
        [9100] = "JetDirect"
    };

    public static string? Guess(IReadOnlyList<int> ports)
    {
        foreach (var port in ports.OrderBy(p => p))
        {
            if (Known.TryGetValue(port, out var name))
            {
                return name;
            }
        }

        return ports.Count > 0 ? FormattableString.Invariant($"tcp/{ports[0]}") : null;
    }
}
