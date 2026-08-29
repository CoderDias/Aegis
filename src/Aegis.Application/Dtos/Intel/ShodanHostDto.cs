namespace Aegis.Application.Dtos.Intel;

public record ShodanHostDto(
    string Ip,
    double Lat,
    double Lng,
    string? Org,
    string? Product,
    int? Port,
    string? Hostnames,
    string? City = null,
    string? Country = null,
    string? Transport = null,
    string? CountryCode = null,
    string? Source = null,
    IReadOnlyList<HostVulnerabilityDto>? Vulnerabilities = null)
{
    public bool HasExploitableVuln =>
        Vulnerabilities?.Any(v => v.HasExploit || v.IsKnownExploited) == true;
}
