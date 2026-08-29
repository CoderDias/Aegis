using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Data.Entities;
using Aegis.Infrastructure.External.Censys;
using Aegis.Infrastructure.Geo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.HostDiscovery;

public sealed class FreeHostDiscoveryClient(
    CountryCidrProvider cidrProvider,
    TcpPortProbe portProbe,
    IpApiGeolocationClient geolocation,
    DiscoveredHostRepository hostRepository,
    IServiceScopeFactory scopeFactory,
    IOptions<HostDiscoveryOptions> options,
    IMemoryCache cache,
    ILogger<FreeHostDiscoveryClient> logger)
{
    public string? LastSearchMessage { get; private set; }

    public async Task<IReadOnlyList<ShodanHostDto>> SearchInViewportAsync(
        BoundingBoxDto bbox,
        int zoom,
        CancellationToken cancellationToken = default)
    {
        _ = zoom;
        LastSearchMessage = null;

        if (!options.Value.Enabled)
        {
            LastSearchMessage = "Descoberta gratuita desabilitada (HostDiscovery:Enabled=false).";
            return [];
        }

        var context = await ViewportHostGeocoding.ResolveAsync(scopeFactory, bbox, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            LastSearchMessage = "Hosts: estado do centro da tela desconhecido.";
            return [];
        }

        var cacheKey = $"hostdiscovery:focus:{context.IngestKey}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<ShodanHostDto>? cached) && cached is not null)
        {
            return FilterVisible(cached, context.VisibleBbox);
        }

        var hosts = await DiscoverFocusAsync(context, cancellationToken).ConfigureAwait(false);
        cache.Set(cacheKey, hosts, TimeSpan.FromHours(24));

        var visible = FilterVisible(hosts, context.VisibleBbox);
        var regionLabel = context.StateRegion ?? context.CountryCode;
        LastSearchMessage = visible.Count > 0
            ? $"Hosts ({regionLabel}): {visible.Count} via probe local."
            : $"Hosts ({regionLabel}): varredura em andamento nesta região.";

        return visible;
    }

    public async Task IngestCountryBatchAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        if (!CountryBoundingBoxCatalog.TryGet(countryCode, out var bbox))
        {
            return;
        }

        var context = ViewportHostFocus.ForCountry(countryCode, bbox);
        var cacheKey = $"hostdiscovery:focus:{context.IngestKey}";
        var hosts = await DiscoverFocusAsync(context, cancellationToken).ConfigureAwait(false);
        cache.Set(cacheKey, hosts, TimeSpan.FromHours(24));

        foreach (var host in hosts)
        {
            if (await hostRepository.ExistsAsync(host.Ip, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            await hostRepository.UpsertAsync(new DiscoveredHostEntity
            {
                Ip = host.Ip,
                CountryCode = context.CountryCode,
                Lat = host.Lat,
                Lng = host.Lng,
                City = host.City,
                Country = host.Country,
                Org = host.Org,
                Port = host.Port,
                Product = host.Product,
                Transport = host.Transport ?? "tcp",
                Source = "Probe",
                IsUp = true,
                LastProbeAt = DateTimeOffset.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<ShodanHostDto>> DiscoverFocusAsync(
        ViewportHostContext context,
        CancellationToken cancellationToken)
    {
        var blocks = await cidrProvider.GetCountryBlocksAsync(context.CountryCode, cancellationToken).ConfigureAwait(false);
        if (blocks.Count == 0)
        {
            return [];
        }

        var sampleCount = Math.Clamp(options.Value.MaxSamplesPerRegion / 3, 4, 12);
        var random = Random.Shared;
        var hosts = new List<ShodanHostDto>();
        var attempts = 0;

        while (hosts.Count < 4 && attempts < sampleCount)
        {
            attempts++;
            var block = blocks[random.Next(blocks.Count)];
            var ip = CidrSampler.SampleIp(block, random);
            var host = await ProbeHostAsync(ip, context, cancellationToken).ConfigureAwait(false);
            if (host is not null)
            {
                hosts.Add(host);
            }
        }

        logger.LogInformation(
            "Host discovery focus ({Region}): {Count} hosts",
            context.IngestKey,
            hosts.Count);

        return hosts;
    }

    private async Task<ShodanHostDto?> ProbeHostAsync(
        string ip,
        ViewportHostContext context,
        CancellationToken cancellationToken)
    {
        if (!options.Value.UseTcpProbe)
        {
            return null;
        }

        var openPorts = await portProbe.ScanAsync(ip, cancellationToken).ConfigureAwait(false);
        if (openPorts.Count == 0)
        {
            return null;
        }

        var geo = await geolocation.LookupAsync(ip, cancellationToken).ConfigureAwait(false);
        if (geo is null ||
            !ViewportHostFocus.MatchesState(context.StateRegion, geo.Region) ||
            !ViewportHostFocus.IsInside(context.FocusBbox, geo.Lat, geo.Lng))
        {
            return null;
        }

        return new ShodanHostDto(
            ip,
            geo.Lat,
            geo.Lng,
            geo.Org ?? geo.Isp,
            PortProductMapper.Guess(openPorts),
            openPorts[0],
            null,
            geo.City,
            geo.Country,
            "tcp");
    }

    private static IReadOnlyList<ShodanHostDto> FilterVisible(
        IReadOnlyList<ShodanHostDto> hosts,
        BoundingBoxDto viewport) =>
        hosts
            .Where(h =>
                h.Lat >= viewport.South && h.Lat <= viewport.North &&
                h.Lng >= viewport.West && h.Lng <= viewport.East)
            .ToList();
}
