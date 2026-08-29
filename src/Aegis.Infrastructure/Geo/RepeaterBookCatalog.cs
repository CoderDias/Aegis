using System.Text.Json;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;
using Aegis.Infrastructure.External.Overpass;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Geo;

public sealed class RepeaterBookCatalog
{
    private readonly object _lock = new();
    private IReadOnlyList<MapFeatureDto> _features = [];
    private readonly ILogger<RepeaterBookCatalog> _logger;
    private readonly RepeaterBookOptions _options;

    public RepeaterBookCatalog(
        IOptions<RepeaterBookOptions> options,
        ILogger<RepeaterBookCatalog> logger)
    {
        _logger = logger;
        _options = options.Value;
        _features = LoadFromFile();
    }

    public IReadOnlyList<MapFeatureDto> GetAll()
    {
        lock (_lock)
        {
            return _features;
        }
    }

    public IReadOnlyList<MapFeatureDto> GetInViewport(BoundingBoxDto bbox)
    {
        lock (_lock)
        {
            return _features
                .Where(f =>
                    f.Centroid.Lat >= bbox.South && f.Centroid.Lat <= bbox.North &&
                    f.Centroid.Lng >= bbox.West && f.Centroid.Lng <= bbox.East)
                .ToList();
        }
    }

    public void Replace(IReadOnlyList<MapFeatureDto> features)
    {
        if (features.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            _features = features;
        }

        _logger.LogInformation("RepeaterBook catalog updated with {Count} repeaters", features.Count);
    }

    private IReadOnlyList<MapFeatureDto> LoadFromFile()
    {
        if (!_options.Enabled)
        {
            return [];
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "repeaterbook-br.json");
            if (!File.Exists(path))
            {
                _logger.LogWarning("RepeaterBook catalog not found at {Path}", path);
                return [];
            }

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<RepeaterBookEntry>>(json, JsonOptions) ?? [];
            return entries
                .Where(e => e.Lat is >= -35 and <= 6 && e.Lng is >= -75 and <= -28)
                .Select(ToFeature)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load RepeaterBook catalog.");
            return [];
        }
    }

    private static MapFeatureDto ToFeature(RepeaterBookEntry entry)
    {
        var callsign = NormalizeCallsign(entry.Callsign, entry.Id);
        var frequency = string.IsNullOrWhiteSpace(entry.Frequency) ? "—" : $"{entry.Frequency} MHz";
        var title = string.IsNullOrWhiteSpace(entry.Location)
            ? $"{callsign} · {frequency}"
            : $"{callsign} · {frequency} — {entry.Location}";

        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = title,
            ["amateur_radio"] = "repeater",
            ["communication:amateur_radio"] = "repeater",
            ["source"] = "repeaterbook",
            ["callsign"] = callsign,
            ["frequency"] = entry.Frequency ?? "",
            ["operational_status"] = entry.OnAir ? "on-air" : "off-air"
        };

        if (!string.IsNullOrWhiteSpace(entry.Mode))
        {
            tags["mode"] = entry.Mode;
        }

        if (!string.IsNullOrWhiteSpace(entry.Location))
        {
            tags["location"] = entry.Location;
        }

        var geometry = JsonSerializer.Serialize(new
        {
            type = "Point",
            coordinates = new[] { entry.Lng, entry.Lat }
        });

        return new MapFeatureDto(
            "repeaterbook",
            entry.Id,
            title,
            entry.Mode ?? "Amateur radio",
            new CoordinateDto(entry.Lat, entry.Lng),
            geometry,
            tags);
    }

    private static string NormalizeCallsign(string? callsign, long id)
    {
        if (string.IsNullOrWhiteSpace(callsign))
        {
            return $"BR-{id}";
        }

        callsign = callsign.Trim();
        if (callsign.StartsWith("ID", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(callsign.AsSpan(2), out _))
        {
            return $"BR-{id}";
        }

        return callsign;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class RepeaterBookEntry
    {
        public long Id { get; set; }
        public string? Callsign { get; set; }
        public string? Frequency { get; set; }
        public string? Location { get; set; }
        public string? Mode { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public bool OnAir { get; set; } = true;
    }
}
