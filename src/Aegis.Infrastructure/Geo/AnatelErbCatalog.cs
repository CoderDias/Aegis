using System.Text.Json;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Geo;

public sealed class AnatelErbCatalog
{
    private readonly IReadOnlyList<MapFeatureDto> _features;
    private readonly ILogger<AnatelErbCatalog> _logger;

    public AnatelErbCatalog(ILogger<AnatelErbCatalog> logger)
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
        if (!TryParseCompositeId(compositeId, "anatel-erb", out var osmId))
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
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "anatel-erb-br.json");
            if (!File.Exists(path))
            {
                _logger.LogWarning("Catálogo ERB/ANATEL não encontrado em {Path}", path);
                return [];
            }

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<ErbEntry>>(json, JsonOptions) ?? [];
            return entries
                .Where(e => e.Lat is >= -35 and <= 6 && e.Lng is >= -75 and <= -28)
                .Select(ToFeature)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar catálogo ERB/ANATEL.");
            return [];
        }
    }

    private static MapFeatureDto ToFeature(ErbEntry entry)
    {
        var title = $"{entry.Technology ?? "ERB"} — {entry.Municipality ?? "BR"}";
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = title,
            ["source"] = "anatel-erb",
            ["telecom"] = "mobile",
            ["technology"] = entry.Technology ?? "unknown",
            ["operator"] = entry.Operator ?? "—"
        };

        if (!string.IsNullOrWhiteSpace(entry.Municipality))
        {
            tags["addr:city"] = entry.Municipality;
        }

        var geometry = JsonSerializer.Serialize(new
        {
            type = "Point",
            coordinates = new[] { entry.Lng, entry.Lat }
        });

        return new MapFeatureDto(
            "anatel-erb",
            entry.Id,
            title,
            "ERB / ANATEL",
            new CoordinateDto(entry.Lat, entry.Lng),
            geometry,
            tags);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class ErbEntry
    {
        public long Id { get; set; }
        public string? Operator { get; set; }
        public string? Technology { get; set; }
        public string? Municipality { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
