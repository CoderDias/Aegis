using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Data.Entities;

namespace Aegis.Infrastructure.External.Censys;

public static class HostDtoMapper
{
    public static ShodanHostDto ToDto(DiscoveredHostEntity entity) =>
        new(
            entity.Ip,
            entity.Lat ?? 0,
            entity.Lng ?? 0,
            entity.Org,
            entity.Product,
            entity.Port,
            null,
            entity.City,
            entity.Country,
            entity.Transport,
            entity.CountryCode,
            entity.Source,
            HostVulnerabilityJson.Deserialize(entity.VulnerabilitiesJson));
}
