using System.Threading.Channels;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Flights;
using Aegis.Application.Geo;
using Aegis.Application.Mapping;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.ValueObjects;
using Aegis.Infrastructure.External.AirStream;
using Aegis.Infrastructure.External.OpenSky;
using Aegis.Application.Settings;
using Aegis.Infrastructure.Options;
using Aegis.Infrastructure.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Background;

public sealed class OpenSkyPollingService : BackgroundService
{
    private const int PurgeEveryCycles = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ChannelWriter<FlightSnapshot> _channelWriter;
    private readonly IViewportBroker _viewportBroker;
    private readonly OpenSkyClient _openSkyClient;
    private readonly AirStreamClient _airStreamClient;
    private readonly AirStreamOptions _airStreamOptions;
    private readonly MapOptions _mapOptions;
    private readonly OpenSkyOptions _openSkyOptions;
    private readonly CacheOptions _cacheOptions;
    private readonly FlightsOptions _flightsOptions;
    private readonly IntegrationSettingsService _integrationSettings;
    private readonly ILogger<OpenSkyPollingService> _logger;

    private int _cycleCount;

    public OpenSkyPollingService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        ChannelWriter<FlightSnapshot> channelWriter,
        IViewportBroker viewportBroker,
        OpenSkyClient openSkyClient,
        AirStreamClient airStreamClient,
        IOptions<MapOptions> mapOptions,
        IOptions<OpenSkyOptions> openSkyOptions,
        IOptions<AirStreamOptions> airStreamOptions,
        IOptions<CacheOptions> cacheOptions,
        IOptions<FlightsOptions> flightsOptions,
        IntegrationSettingsService integrationSettings,
        ILogger<OpenSkyPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _channelWriter = channelWriter;
        _viewportBroker = viewportBroker;
        _openSkyClient = openSkyClient;
        _airStreamClient = airStreamClient;
        _airStreamOptions = airStreamOptions.Value;
        _mapOptions = mapOptions.Value;
        _openSkyOptions = openSkyOptions.Value;
        _cacheOptions = cacheOptions.Value;
        _flightsOptions = flightsOptions.Value;
        _integrationSettings = integrationSettings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_viewportBroker.HasActiveViewers)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_openSkyOptions.PollingIntervalSeconds), stoppingToken)
                        .ConfigureAwait(false);
                    continue;
                }

                var activeLayers = _viewportBroker.ActiveLayers;
                if (activeLayers is not null && !activeLayers.Aircraft)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_openSkyOptions.PollingIntervalSeconds), stoppingToken)
                        .ConfigureAwait(false);
                    continue;
                }

                var viewport = _viewportBroker.Last;
                var bbox = ResolveBoundingBox(viewport);
                var capturedAt = DateTimeOffset.UtcNow;
                var aircraftDict = MergeWithCachedAircraft();
                IReadOnlyList<OpenSkyStateVector> vectors = [];

                if (_integrationSettings.IsEnabled(IntegrationKeys.OpenSky))
                {
                    var raw = await _openSkyClient.GetStatesRawAsync(bbox, stoppingToken).ConfigureAwait(false);
                    vectors = OpenSkyStateVectorParser.ParseStatesJson(raw);

                    foreach (var marker in vectors
                                 .Where(v => v.Latitude is not null && v.Longitude is not null)
                                 .Select(OpenSkyStateVectorParser.ToMarkerDto))
                    {
                        aircraftDict[marker.Icao24] = marker;
                    }
                }

                if (_integrationSettings.IsEnabled(IntegrationKeys.AirStream) &&
                    _airStreamOptions.Enabled &&
                    viewport is not null)
                {
                    var centerLat = (bbox.South + bbox.North) / 2;
                    var centerLng = (bbox.West + bbox.East) / 2;
                    var airStream = await _airStreamClient
                        .GetAircraftInRadiusAsync(centerLat, centerLng, _airStreamOptions.RadiusNm, stoppingToken)
                        .ConfigureAwait(false);

                    foreach (var ac in airStream)
                    {
                        aircraftDict.TryAdd(ac.Icao24, ac);
                    }
                }

                var aircraft = aircraftDict.Values
                    .Take(_flightsOptions.MaxMarkers * 3)
                    .ToList();

                var snapshot = new FlightSnapshot(capturedAt, bbox.ToDto(), aircraft);
                var cacheEntry = new CachedFlightSnapshot(capturedAt, bbox.ToDto(), aircraft);

                _memoryCache.Set(
                    FlightTrackingService.CacheKey,
                    cacheEntry,
                    TimeSpan.FromMinutes(_cacheOptions.FlightDataTtlMinutes));

                _channelWriter.TryWrite(snapshot);

                await PersistTrackPointsAsync(aircraft, capturedAt, stoppingToken).ConfigureAwait(false);
                await MaybePurgeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenSky polling cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_openSkyOptions.PollingIntervalSeconds), stoppingToken)
                .ConfigureAwait(false);
        }
    }

    private BoundingBox ResolveBoundingBox(Viewport? viewport)
    {
        if (viewport is not null)
        {
            var dto = viewport.Box.ToDto();
            var fetchZoom = ViewportTileGrid.FetchZoomLevel(viewport.Zoom.Value);
            var snapped = ViewportTileGrid.SnapToTiles(dto, fetchZoom);
            return BoundingBox.Create(snapped.South, snapped.West, snapped.North, snapped.East);
        }

        var span = 5d;
        return BoundingBox.Create(
            _mapOptions.DefaultLat - span,
            _mapOptions.DefaultLng - span,
            _mapOptions.DefaultLat + span,
            _mapOptions.DefaultLng + span);
    }

    private Dictionary<string, AircraftMarkerDto> MergeWithCachedAircraft()
    {
        if (!_memoryCache.TryGetValue(FlightTrackingService.CacheKey, out CachedFlightSnapshot? snapshot) ||
            snapshot is null)
        {
            return new Dictionary<string, AircraftMarkerDto>(StringComparer.OrdinalIgnoreCase);
        }

        return snapshot.Aircraft.ToDictionary(a => a.Icao24, a => a, StringComparer.OrdinalIgnoreCase);
    }

    private async Task PersistTrackPointsAsync(
        IReadOnlyList<AircraftMarkerDto> aircraft,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        var points = aircraft
            .Where(a => a.Icao24.Length == 6 && a.Icao24.All(Uri.IsHexDigit))
            .Select(a => FlightTrackPoint.Create(
                0,
                a.Icao24,
                capturedAt,
                a.Lat,
                a.Lng,
                DataSourceType.AirStream,
                a.Callsign,
                a.BaroAltitude,
                null,
                a.Velocity,
                a.Heading,
                null,
                a.OriginCountry,
                a.OnGround))
            .ToList();

        if (points.Count == 0)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IFlightSnapshotStore>();
        await store.InsertPointsAsync(points, cancellationToken).ConfigureAwait(false);
    }

    private async Task MaybePurgeAsync(CancellationToken cancellationToken)
    {
        _cycleCount++;
        if (_cycleCount % PurgeEveryCycles != 0)
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_flightsOptions.RetentionDays);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IFlightSnapshotStore>();
        await store.PurgeOldAsync(cutoff, cancellationToken).ConfigureAwait(false);
    }
}
