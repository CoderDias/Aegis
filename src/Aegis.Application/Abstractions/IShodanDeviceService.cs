using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;

namespace Aegis.Application.Abstractions;

public interface IShodanDeviceService
{
    string? LastSearchMessage { get; }

    Task<IReadOnlyList<ShodanHostDto>> SearchInViewportAsync(
        BoundingBoxDto bbox,
        int zoom,
        CancellationToken cancellationToken = default);

    Task<bool> ProbeAsync(CancellationToken cancellationToken = default);

    Task<ShodanApiInfoDto?> GetApiInfoAsync(CancellationToken cancellationToken = default);

    bool IsConfigured { get; }
}
