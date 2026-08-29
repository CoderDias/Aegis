using System.Globalization;
using System.Text;
using System.Text.Json;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Censys;

public sealed record CensysHostRecord(
    string Ip,
    double? Lat,
    double? Lng,
    string? City,
    string? Country,
    string? CountryCode,
    string? Org,
    int? Port,
    string? Product,
    string? Transport,
    IReadOnlyList<HostVulnerabilityDto>? Vulnerabilities = null);

public sealed record CensysSearchPage(
    IReadOnlyList<CensysHostRecord> Hosts,
    string? NextPageToken);

public sealed class CensysClient(
    IHttpClientFactory httpClientFactory,
    IOptions<CensysOptions> options,
    CensysQuotaService quota,
    ILogger<CensysClient> logger)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiToken);

    public bool CanUseSearch =>
        IsConfigured &&
        options.Value.AllowSearchApi &&
        !string.IsNullOrWhiteSpace(options.Value.OrganizationId);

    public async Task<CensysHostRecord?> LookupHostAsync(string ip, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || !await quota.TryConsumeAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientNames.Censys);
            var url = $"global/asset/host/{Uri.EscapeDataString(ip)}";
            if (!string.IsNullOrWhiteSpace(options.Value.OrganizationId))
            {
                url += $"?organization_id={Uri.EscapeDataString(options.Value.OrganizationId)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Value.ApiToken);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Censys host lookup failed for {Ip}: {Status}", ip, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseHostAsset(json, ip);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Censys host lookup error for {Ip}", ip);
            return null;
        }
    }

    public async Task<CensysSearchPage?> SearchCountryPageAsync(
        string countryCode,
        string? pageToken,
        CancellationToken cancellationToken = default)
    {
        if (!CanUseSearch || !await quota.TryConsumeAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var query = FormattableString.Invariant(
            $"host.location.country_code: \"{countryCode.ToUpperInvariant()}\"");
        var payload = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["page_size"] = Math.Clamp(options.Value.SearchPageSize, 1, 100),
            ["fields"] = new[]
            {
                "host.ip",
                "host.location",
                "host.services.port",
                "host.services.protocol",
                "host.services.transport_protocol",
                "host.services.vulnerabilities",
                "host.autonomous_system"
            }
        };

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            payload["page_token"] = pageToken;
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientNames.Censys);
            var url = "global/search/query";
            if (!string.IsNullOrWhiteSpace(options.Value.OrganizationId))
            {
                url += $"?organization_id={Uri.EscapeDataString(options.Value.OrganizationId)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Value.ApiToken);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning(
                    "Censys search failed for {Country}: {Status} {Body}",
                    countryCode,
                    response.StatusCode,
                    body.Length > 200 ? body[..200] : body);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseSearchPage(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Censys search error for {Country}", countryCode);
            return null;
        }
    }

    internal static CensysHostRecord? ParseHostAsset(string json, string fallbackIp)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("resource", out var resource))
        {
            return null;
        }

        return ParseHostResource(resource, fallbackIp);
    }

    internal static CensysSearchPage? ParseSearchPage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var hosts = new List<CensysHostRecord>();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                if (item.TryGetProperty("host", out var host))
                {
                    var ip = host.TryGetProperty("ip", out var ipProp) ? ipProp.GetString() : null;
                    if (!string.IsNullOrEmpty(ip))
                    {
                        var record = ParseHostResource(host, ip);
                        if (record is not null)
                        {
                            hosts.Add(record);
                        }
                    }
                }
            }
        }

        string? next = root.TryGetProperty("next_page_token", out var tokenProp) &&
                       tokenProp.ValueKind == JsonValueKind.String
            ? tokenProp.GetString()
            : null;

        return new CensysSearchPage(hosts, next);
    }

    private static CensysHostRecord? ParseHostResource(JsonElement resource, string ip)
    {
        double? lat = null;
        double? lng = null;
        string? city = null;
        string? country = null;
        string? countryCode = null;

        if (resource.TryGetProperty("location", out var location))
        {
            city = location.TryGetProperty("city", out var cityProp) ? cityProp.GetString() : null;
            country = location.TryGetProperty("country", out var countryProp) ? countryProp.GetString() : null;
            countryCode = location.TryGetProperty("country_code", out var ccProp)
                ? ccProp.GetString()?.ToUpperInvariant()
                : null;

            if (location.TryGetProperty("coordinates", out var coords))
            {
                if (coords.TryGetProperty("latitude", out var latProp))
                {
                    lat = latProp.GetDouble();
                }

                if (coords.TryGetProperty("longitude", out var lngProp))
                {
                    lng = lngProp.GetDouble();
                }
            }
        }

        string? org = null;
        if (resource.TryGetProperty("autonomous_system", out var asn) &&
            asn.TryGetProperty("description", out var asnDesc))
        {
            org = asnDesc.GetString();
        }

        int? port = null;
        string? product = null;
        string? transport = null;
        var vulnerabilities = new List<HostVulnerabilityDto>();
        if (resource.TryGetProperty("services", out var services) && services.ValueKind == JsonValueKind.Array)
        {
            foreach (var svc in services.EnumerateArray())
            {
                if (port is null && svc.TryGetProperty("port", out var portProp) && portProp.ValueKind == JsonValueKind.Number)
                {
                    port = portProp.GetInt32();
                    product = svc.TryGetProperty("protocol", out var protoProp) ? protoProp.GetString() : null;
                    transport = svc.TryGetProperty("transport_protocol", out var tpProp)
                        ? tpProp.GetString()
                        : "tcp";
                }

                if (svc.TryGetProperty("vulnerabilities", out var vulns) && vulns.ValueKind == JsonValueKind.Array)
                {
                    foreach (var vuln in vulns.EnumerateArray())
                    {
                        var parsed = ParseVulnerability(vuln);
                        if (parsed is not null)
                        {
                            vulnerabilities.Add(parsed);
                        }
                    }
                }
            }
        }

        return new CensysHostRecord(
            ip,
            lat,
            lng,
            city,
            country,
            countryCode,
            org,
            port,
            product,
            transport,
            vulnerabilities.Count > 0 ? vulnerabilities : null);
    }

    private static HostVulnerabilityDto? ParseVulnerability(JsonElement vuln)
    {
        var cveId = vuln.TryGetProperty("cve_id", out var cveProp) ? cveProp.GetString()
            : vuln.TryGetProperty("cve", out var cveAlt) ? cveAlt.GetString()
            : vuln.TryGetProperty("id", out var idProp) ? idProp.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(cveId))
        {
            return null;
        }

        var severity = vuln.TryGetProperty("severity", out var sevProp) ? sevProp.GetString()
            : vuln.TryGetProperty("risk", out var riskProp) ? riskProp.GetString()
            : null;

        var hasExploit = ReadBool(vuln, "exploit_available")
            || ReadBool(vuln, "has_exploit")
            || ReadBool(vuln, "in_the_wild");

        var isKnownExploited = ReadBool(vuln, "is_known_exploited_vulnerability")
            || ReadBool(vuln, "known_exploited")
            || ReadBool(vuln, "kev");

        return new HostVulnerabilityDto(cveId, severity, hasExploit, isKnownExploited);
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(prop.GetString(), out var b) && b,
            JsonValueKind.Number => prop.GetInt32() != 0,
            _ => false
        };
    }
}
