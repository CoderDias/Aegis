using System.Text.Json;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Geo;

public sealed class RepeaterBookRefreshService(
    RepeaterBookClient client,
    RepeaterBookCatalog catalog,
    IOptions<RepeaterBookOptions> options,
    ILogger<RepeaterBookRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!options.Value.Enabled)
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                var repeaters = await client.FetchBrazilRepeatersAsync(stoppingToken).ConfigureAwait(false);
                if (repeaters.Count > 0)
                {
                    catalog.Replace(repeaters.Select(ToFeature).ToList());
                    await PersistAsync(repeaters, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "RepeaterBook refresh failed.");
            }

            var hours = Math.Clamp(options.Value.RefreshHours, 24, 720);
            await Task.Delay(TimeSpan.FromHours(hours), stoppingToken).ConfigureAwait(false);
        }
    }

    private static async Task PersistAsync(
        IReadOnlyList<RepeaterBookCatalogEntry> repeaters,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "repeaterbook-br.json");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            repeaters,
            new JsonSerializerOptions { WriteIndented = true },
            cancellationToken).ConfigureAwait(false);
    }

    private static MapFeatureDto ToFeature(RepeaterBookCatalogEntry entry)
    {
        var callsign = string.IsNullOrWhiteSpace(entry.Callsign) ? $"BR-{entry.Id}" : entry.Callsign.Trim();
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
}
