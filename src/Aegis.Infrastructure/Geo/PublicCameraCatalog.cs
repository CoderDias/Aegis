using System.Text.Json;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;
using Aegis.Application.Dtos.Osint;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Geo;

public sealed class PublicCameraCatalog
{
    private readonly IReadOnlyList<MapFeatureDto> _features;
    private readonly ILogger<PublicCameraCatalog> _logger;

    public PublicCameraCatalog(IOsintBrazucaCatalog osintCatalog, ILogger<PublicCameraCatalog> logger)
    {
        _logger = logger;
        _features = LoadFeatures(osintCatalog);
        _logger.LogInformation("Catálogo de câmeras públicas carregado com {Count} fontes.", _features.Count);
    }

    public IReadOnlyList<MapFeatureDto> GetInViewport(BoundingBoxDto bbox) =>
        _features
            .Where(f =>
                f.Centroid.Lat >= bbox.South && f.Centroid.Lat <= bbox.North &&
                f.Centroid.Lng >= bbox.West && f.Centroid.Lng <= bbox.East)
            .ToList();

    public MapFeatureDto? TryGetByCompositeId(string compositeId)
    {
        if (!TryParseCompositeId(compositeId, out var osmId))
        {
            return null;
        }

        return _features.FirstOrDefault(f => f.OsmId == osmId);
    }

    private static bool TryParseCompositeId(string compositeId, out long osmId)
    {
        osmId = 0;
        var slash = compositeId.IndexOf('/');
        if (slash <= 0 || slash >= compositeId.Length - 1)
        {
            return false;
        }

        if (!string.Equals(compositeId[..slash], "brazuca-camera", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return long.TryParse(compositeId[(slash + 1)..], out osmId);
    }

    private IReadOnlyList<MapFeatureDto> LoadFeatures(IOsintBrazucaCatalog osintCatalog)
    {
        try
        {
            var sources = osintCatalog.Search(new OsintSearchQuery(
                CategoriaId: "cameras-online",
                Limit: 200));

            return sources
                .Select((source, index) => ToFeature(source, index + 1))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar catálogo de câmeras públicas.");
            return [];
        }
    }

    private static MapFeatureDto ToFeature(OsintSourceDto source, int sequence)
    {
        var uf = source.Uf ?? BrazilStateCentroids.InferUfFromUrl(source.Url);
        var stableKey = source.FonteId;
        var centroid = BrazilStateCentroids.Resolve(uf, stableKey);
        var linkType = PublicCameraLinkClassifier.Classify(source.Url);
        var osmId = StableId(source.FonteId, source.Url, sequence);

        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = source.Fonte,
            ["source"] = "brazuca-camera",
            ["url"] = source.Url,
            ["surveillance"] = "public",
            ["fonte_id"] = source.FonteId,
            ["link_type"] = linkType,
            ["link_label"] = PublicCameraLinkClassifier.DescribeLinkType(linkType)
        };

        if (!string.IsNullOrWhiteSpace(source.Descricao))
        {
            tags["description"] = source.Descricao;
        }

        if (!string.IsNullOrWhiteSpace(uf))
        {
            tags["addr:state"] = uf;
        }

        if (!string.IsNullOrWhiteSpace(source.TipoFonte))
        {
            tags["operator"] = source.TipoFonte;
        }

        var geometry = JsonSerializer.Serialize(new
        {
            type = "Point",
            coordinates = new[] { centroid.Lng, centroid.Lat }
        });

        return new MapFeatureDto(
            "brazuca-camera",
            osmId,
            source.Fonte,
            PublicCameraLinkClassifier.DescribeLinkType(linkType),
            centroid,
            geometry,
            tags);
    }

    private static long StableId(string fonteId, string url, int sequence)
    {
        var hash = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode($"{fonteId}|{url}"));
        if (hash == 0)
        {
            return sequence;
        }

        return hash;
    }
}
