using System.Text.Json;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Geo;

public sealed class StaticGovernmentPoiCatalog
{
    private readonly IReadOnlyList<MapFeatureDto> _features;
    private readonly ILogger<StaticGovernmentPoiCatalog> _logger;

    public StaticGovernmentPoiCatalog(ILogger<StaticGovernmentPoiCatalog> logger)
    {
        _logger = logger;
        _features = LoadFeatures();
    }

    public IReadOnlyList<MapFeatureDto> GetInViewport(BoundingBoxDto bbox) =>
        _features
            .Where(f =>
                f.Centroid.Lat >= bbox.South && f.Centroid.Lat <= bbox.North &&
                f.Centroid.Lng >= bbox.West && f.Centroid.Lng <= bbox.East)
            .ToList();

    private IReadOnlyList<MapFeatureDto> LoadFeatures()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "government-pois-br.json");
            if (!File.Exists(path))
            {
                _logger.LogWarning("Catálogo estático de POIs não encontrado em {Path}", path);
                return [];
            }

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<StaticPoiEntry>>(json, JsonOptions) ?? [];
            return entries
                .Select((entry, index) => ToFeature(entry, index + 1))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar catálogo estático de POIs.");
            return [];
        }
    }

    private static MapFeatureDto ToFeature(StaticPoiEntry entry, long id)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = entry.Name,
            ["amenity"] = entry.Amenity
        };

        if (!string.IsNullOrWhiteSpace(entry.Office))
        {
            tags["office"] = entry.Office;
        }

        if (!string.IsNullOrWhiteSpace(entry.Military))
        {
            tags["military"] = entry.Military;
        }

        var geometry = JsonSerializer.Serialize(new
        {
            type = "Point",
            coordinates = new[] { entry.Lng, entry.Lat }
        });

        return new MapFeatureDto(
            "static",
            id,
            entry.Name,
            entry.Amenity,
            new CoordinateDto(entry.Lat, entry.Lng),
            geometry,
            tags);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class StaticPoiEntry
    {
        public string Name { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string Amenity { get; set; } = "public_building";
        public string? Office { get; set; }
        public string? Military { get; set; }
    }
}
