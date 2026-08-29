using Aegis.Application.Dtos.Geo;

namespace Aegis.Application.Abstractions;

public interface IGeocodingService
{
    Task<IReadOnlyList<GeocodeResultDto>> SearchAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task<GeocodeResultDto?> ReverseAsync(
        CoordinateDto coordinate,
        CancellationToken cancellationToken = default);
}
