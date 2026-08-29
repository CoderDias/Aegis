using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Dtos.Investigations;
using Aegis.Application.Dtos.Osint;
using Aegis.Domain.Enums;

namespace Aegis.Application.Osint;

public static partial class OsintContextExtractor
{
    public static OsintContext FromSelection(string? kind, object? payload) =>
        kind switch
        {
            "shodan" when payload is ShodanHostDto host => FromHost(host),
            "aircraft" when payload is AircraftMarkerDto aircraft => FromAircraft(aircraft),
            "asset" when payload is AssetDto asset => FromAsset(asset),
            "geocode" when payload is GeocodeResultDto geocode => FromGeocode(geocode),
            _ => new OsintContext()
        };

    public static OsintContext FromAssetMetadata(AssetType type, string displayName, string metadataJson)
    {
        var context = new OsintContext();
        var cnpj = NormalizeCnpj(displayName) ?? ExtractCnpjFromJson(metadataJson);
        if (cnpj is not null)
        {
            return context with { Cnpj = cnpj };
        }

        var domain = ExtractDomain(displayName);
        if (domain is not null)
        {
            return context with { Domain = domain };
        }

        if (type == AssetType.Host && IpRegex().IsMatch(displayName))
        {
            return context with { Ip = displayName.Trim() };
        }

        return context;
    }

    private static OsintContext FromHost(ShodanHostDto host)
    {
        var domain = ExtractDomain(host.Hostnames) ?? ExtractDomain(host.Org);
        return new OsintContext(
            Domain: domain,
            Ip: host.Ip,
            Uf: host.CountryCode?.Equals("BR", StringComparison.OrdinalIgnoreCase) == true ? null : host.CountryCode);
    }

    private static OsintContext FromAircraft(AircraftMarkerDto aircraft) =>
        new(Placa: aircraft.Callsign?.Trim());

    private static OsintContext FromAsset(AssetDto asset) =>
        FromAssetMetadata(asset.Type, asset.DisplayName, asset.MetadataJson);

    private static OsintContext FromGeocode(GeocodeResultDto geocode)
    {
        var parts = geocode.AddressParts;
        var uf = parts?.GetValueOrDefault("state");
        var cep = parts?.GetValueOrDefault("postcode");
        return new OsintContext(
            Cep: NormalizeCep(cep),
            Uf: uf);
    }

    public static string? NormalizeCnpj(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 14 ? digits : null;
    }

    public static string? NormalizeCep(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? digits : null;
    }

    private static string? ExtractCnpjFromJson(string metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.Contains("cnpj", StringComparison.OrdinalIgnoreCase) &&
                    prop.Value.ValueKind == JsonValueKind.String)
                {
                    return NormalizeCnpj(prop.Value.GetString());
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string? ExtractDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var token = value.Split(',', ';', ' ', '\n', '\r', '\t').FirstOrDefault(t => t.Contains('.'));
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        token = token.Trim().TrimEnd('.');
        if (!token.Contains('.') || token.Contains(' '))
        {
            return null;
        }

        return token.ToLowerInvariant();
    }

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}$")]
    private static partial Regex IpRegex();
}
