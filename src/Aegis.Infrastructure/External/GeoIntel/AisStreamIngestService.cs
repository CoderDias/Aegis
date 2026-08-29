using System.Collections.Concurrent;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Intel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.GeoIntel;

public sealed class AisStreamIngestService(
    IOptions<GeoIntelOptions> options,
    IGeoIntelCache cache,
    IViewportBroker viewportBroker,
    ILogger<AisStreamIngestService> logger) : BackgroundService
{
    private const string StreamUrl = "wss://stream.aisstream.io/v0/stream";
    private readonly ConcurrentDictionary<string, GeoMarkerDto> _vessels = new(StringComparer.OrdinalIgnoreCase);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.AisStreamApiKey);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!ShouldRun())
            {
                _vessels.Clear();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                await RunStreamSessionAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AISStream session ended unexpectedly");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private bool ShouldRun()
    {
        if (!options.Value.Enabled || !IsConfigured || !viewportBroker.HasActiveViewers)
        {
            return false;
        }

        return viewportBroker.ActiveLayers?.Ships == true;
    }

    private async Task RunStreamSessionAsync(CancellationToken stoppingToken)
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(StreamUrl), stoppingToken).ConfigureAwait(false);

        var subscription = JsonSerializer.Serialize(new
        {
            APIKey = options.Value.AisStreamApiKey!.Trim(),
            BoundingBoxes = new[] { new[] { new[] { -90.0, -180.0 }, new[] { 90.0, 180.0 } } },
            FilterMessageTypes = new[] { "PositionReport", "ShipStaticData" }
        });

        await ws.SendAsync(
            Encoding.UTF8.GetBytes(subscription),
            WebSocketMessageType.Text,
            true,
            stoppingToken).ConfigureAwait(false);

        logger.LogInformation("AISStream connected and subscribed");

        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var flushTask = PeriodicFlushAsync(sessionCts.Token);

        try
        {
            await ReceiveLoopAsync(ws, sessionCts.Token).ConfigureAwait(false);
        }
        finally
        {
            sessionCts.Cancel();
            try
            {
                await flushTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[32768];

        while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            if (!ShouldRun())
            {
                break;
            }

            var result = await ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            TryIngestMessage(text);
        }
    }

    private async Task PeriodicFlushAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (ShouldRun())
            {
                FlushToCache();
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
        }
    }

    private void TryIngestMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("MessageType", out var typeProp))
            {
                return;
            }

            var messageType = typeProp.GetString();
            if (string.Equals(messageType, "PositionReport", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParsePositionReport(root, out var marker) && marker is not null)
                {
                    _vessels[marker.Id] = marker;
                }
            }
            else if (string.Equals(messageType, "ShipStaticData", StringComparison.OrdinalIgnoreCase) &&
                     TryParseShipStatic(root, out var staticMarker) &&
                     staticMarker is not null)
            {
                _vessels.AddOrUpdate(
                    staticMarker.Id,
                    staticMarker,
                    (_, existing) => existing with
                    {
                        Title = staticMarker.Title,
                        Subtitle = staticMarker.Subtitle ?? existing.Subtitle
                    });
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "AISStream frame parse failed");
        }
    }

    private void FlushToCache()
    {
        if (viewportBroker.Last is null)
        {
            return;
        }

        var max = Math.Clamp(options.Value.MaxShipMarkers, 50, 5000);
        var markers = _vessels.Values
            .OrderByDescending(v => v.Timestamp ?? DateTimeOffset.MinValue)
            .Take(max)
            .ToList();

        if (markers.Count == 0)
        {
            return;
        }

        cache.SetShips(markers, BboxKey(viewportBroker.Last));
        logger.LogDebug("AISStream cache flush: {Count} vessels", markers.Count);
    }

    private static string BboxKey(Viewport viewport)
    {
        var box = viewport.Box;
        return $"{box.South:F2}:{box.West:F2}:{box.North:F2}:{box.East:F2}";
    }

    internal static bool TryParsePositionReport(JsonElement root, out GeoMarkerDto? marker)
    {
        marker = null;
        if (!root.TryGetProperty("MetaData", out var meta) ||
            !TryGetDouble(meta, "latitude", out var lat) ||
            !TryGetDouble(meta, "longitude", out var lng))
        {
            return false;
        }

        var id = TryGetMmsi(meta);
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var name = TryGetString(meta, "ShipName")?.Trim();
        var timestamp = TryParseUtc(TryGetString(meta, "time_utc"));

        double? sog = null;
        double? cog = null;
        if (root.TryGetProperty("Message", out var message) &&
            message.TryGetProperty("PositionReport", out var report))
        {
            if (TryGetDouble(report, "Sog", out var sogValue))
            {
                sog = sogValue;
            }

            if (TryGetDouble(report, "Cog", out var cogValue))
            {
                cog = cogValue;
            }
        }

        marker = new GeoMarkerDto(
            id,
            "ship",
            string.IsNullOrWhiteSpace(name) ? id : name!,
            sog is not null ? $"{sog.Value:F1} kn" : null,
            lat,
            lng,
            1.2,
            timestamp,
            cog is not null ? $"Curso {cog.Value:F0}°" : null);
        return true;
    }

    internal static bool TryParseShipStatic(JsonElement root, out GeoMarkerDto? marker)
    {
        marker = null;
        if (!root.TryGetProperty("MetaData", out var meta))
        {
            return false;
        }

        var id = TryGetMmsi(meta);
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var name = TryGetString(meta, "ShipName")?.Trim();
        if (string.IsNullOrWhiteSpace(name) &&
            root.TryGetProperty("Message", out var message) &&
            message.TryGetProperty("ShipStaticData", out var shipStatic))
        {
            name = TryGetString(shipStatic, "Name")?.Trim();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (!TryGetDouble(meta, "latitude", out var lat) ||
            !TryGetDouble(meta, "longitude", out var lng))
        {
            return false;
        }

        marker = new GeoMarkerDto(
            id,
            "ship",
            name,
            TryGetString(meta, "CallSign"),
            lat,
            lng,
            1.0,
            TryParseUtc(TryGetString(meta, "time_utc")));
        return true;
    }

    private static string? TryGetMmsi(JsonElement meta)
    {
        if (meta.TryGetProperty("MMSI_String", out var mmsiString) &&
            mmsiString.ValueKind == JsonValueKind.String)
        {
            return mmsiString.GetString();
        }

        if (meta.TryGetProperty("MMSI", out var mmsi))
        {
            return mmsi.ValueKind switch
            {
                JsonValueKind.Number => mmsi.GetRawText(),
                JsonValueKind.String => mmsi.GetString(),
                _ => null
            };
        }

        return null;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            foreach (var item in element.EnumerateObject())
            {
                if (string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    prop = item.Value;
                    break;
                }
            }
        }

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    private static bool TryGetDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            foreach (var item in element.EnumerateObject())
            {
                if (string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    prop = item.Value;
                    break;
                }
            }
        }

        if (prop.ValueKind == JsonValueKind.Number)
        {
            value = prop.GetDouble();
            return true;
        }

        return prop.ValueKind == JsonValueKind.String &&
               double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static DateTimeOffset? TryParseUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
