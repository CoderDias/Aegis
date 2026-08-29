using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Microsoft.Extensions.DependencyInjection;

namespace Aegis.Infrastructure.External.HostDiscovery;

public static class ViewportHostGeocoding
{
    public static async Task<ViewportHostContext?> ResolveAsync(
        IServiceScopeFactory scopeFactory,
        BoundingBoxDto bbox,
        CancellationToken cancellationToken)
    {
        var centerLat = (bbox.South + bbox.North) / 2;
        var centerLng = (bbox.West + bbox.East) / 2;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var geocoding = scope.ServiceProvider.GetRequiredService<IGeocodingService>();
            var geocode = await geocoding
                .ReverseAsync(new CoordinateDto(centerLat, centerLng), cancellationToken)
                .ConfigureAwait(false);

            var country = geocode?.AddressParts?.GetValueOrDefault("country_code")?.ToUpperInvariant()
                ?? GuessCountryFromCoords(centerLat, centerLng);
            if (string.IsNullOrWhiteSpace(country))
            {
                return null;
            }

            var state = geocode?.AddressParts?.GetValueOrDefault("state")
                ?? geocode?.AddressParts?.GetValueOrDefault("region")
                ?? geocode?.AddressParts?.GetValueOrDefault("state_district");

            return ViewportHostFocus.Create(bbox, country, state);
        }
        catch
        {
            var country = GuessCountryFromCoords(centerLat, centerLng);
            return country is null ? null : ViewportHostFocus.Create(bbox, country, null);
        }
    }

    private static string? GuessCountryFromCoords(double lat, double lng)
    {
        if (lat is >= -34 and <= 5 && lng is >= -74 and <= -34)
        {
            return "BR";
        }

        if (lat is >= -56 and <= -21 && lng is >= -74 and <= -53)
        {
            return "AR";
        }

        return null;
    }
}
