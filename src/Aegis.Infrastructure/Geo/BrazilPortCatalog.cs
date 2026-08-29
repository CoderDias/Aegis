using System.Text.Json;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Geo;

public sealed class BrazilPortCatalog
{
    private readonly IReadOnlyList<MapFeatureDto> _features;
    private readonly ILogger<BrazilPortCatalog> _logger;

    public BrazilPortCatalog(ILogger<BrazilPortCatalog> logger)
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

    public MapFeatureDto? TryGetByCompositeId(string compositeId)
    {
        if (!TryParseCompositeId(compositeId, "brazuca-port", out var osmId))
        {
            return null;
        }

        return _features.FirstOrDefault(f => f.OsmId == osmId);
    }

    private static bool TryParseCompositeId(string compositeId, string expectedType, out long osmId)
    {
        osmId = 0;
        var slash = compositeId.IndexOf('/');
        if (slash <= 0 || slash >= compositeId.Length - 1)
        {
            return false;
        }

        if (!string.Equals(compositeId[..slash], expectedType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return long.TryParse(compositeId[(slash + 1)..], out osmId);
    }

    private IReadOnlyList<MapFeatureDto> LoadFeatures()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "brazuca-ports-br.json");
            if (!File.Exists(path))
            {
                _logger.LogWarning("Catálogo de portos BR não encontrado em {Path}", path);
                return [];
            }

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<PortEntry>>(json, JsonOptions) ?? [];
            return entries.Select(ToFeature).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar catálogo de portos BR.");
            return [];
        }
    }

    private static MapFeatureDto ToFeature(PortEntry entry)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = entry.Name,
            ["source"] = "brazuca-port",
            ["url"] = entry.Url,
            ["harbour"] = "yes"
        };

        if (!string.IsNullOrWhiteSpace(entry.Uf))
        {
            tags["addr:state"] = entry.Uf;
        }

        var geometry = JsonSerializer.Serialize(new
        {
            type = "Point",
            coordinates = new[] { entry.Lng, entry.Lat }
        });

        return new MapFeatureDto(
            "brazuca-port",
            entry.Id,
            entry.Name,
            "Porto / ANTAQ",
            new CoordinateDto(entry.Lat, entry.Lng),
            geometry,
            tags);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class PortEntry
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Uf { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
