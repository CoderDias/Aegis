using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Data.Entities;
using Aegis.Infrastructure.External.HostDiscovery;
using Aegis.Infrastructure.Geo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Censys;

public sealed class CensysHostDiscoveryClient(
    CensysClient censys,
    CensysQuotaService quota,
    DiscoveredHostRepository repository,
    CountryCidrProvider cidrProvider,
    InternetDbClient internetDb,
    TcpPortProbe portProbe,
    IpApiGeolocationClient geolocation,
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    IOptions<CensysOptions> options,
    ILogger<CensysHostDiscoveryClient> logger)
{
    private const string CidrCursorCachePrefix = "host-ingest:cursor:";

    public string? LastSearchMessage { get; private set; }

    public async Task<IReadOnlyList<ShodanHostDto>> SearchInViewportAsync(
        BoundingBoxDto bbox,
        int zoom,
        CancellationToken cancellationToken = default)
    {
        _ = zoom;
        LastSearchMessage = null;

        if (!options.Value.Enabled || !censys.IsConfigured)
        {
            LastSearchMessage = "Censys: configure Censys:ApiToken.";
            return [];
        }

        var context = await ViewportHostGeocoding.ResolveAsync(scopeFactory, bbox, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            LastSearchMessage = "Hosts: estado/região do centro da tela desconhecido.";
            return [];
        }

        await IngestFocusBatchAsync(context, cancellationToken).ConfigureAwait(false);
        await RefreshReachabilityAsync(context, cancellationToken).ConfigureAwait(false);
        await EnrichWithCensysLookupsAsync(context, cancellationToken).ConfigureAwait(false);

        var visible = context.VisibleBbox;
        var entities = await repository
            .ListInViewportAsync(
                context.CountryCode,
                visible.South,
                visible.North,
                visible.West,
                visible.East,
                cancellationToken)
            .ConfigureAwait(false);

        var (used, max) = await quota.GetUsageAsync(cancellationToken).ConfigureAwait(false);
        var regionLabel = context.StateRegion ?? context.CountryCode;
        LastSearchMessage = entities.Count > 0
            ? $"Hosts ({regionLabel}): {entities.Count} no mapa ({used}/{max} Censys/mês)."
            : $"Hosts ({regionLabel}): varredura no centro da tela ({used}/{max} Censys/mês). Aguarde…";

        return entities.Select(HostDtoMapper.ToDto).ToList();
    }

    public async Task IngestCountryBatchAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || !censys.IsConfigured)
        {
            return;
        }

        if (!CountryBoundingBoxCatalog.TryGet(countryCode, out var bbox))
        {
            return;
        }

        var context = ViewportHostFocus.ForCountry(countryCode, bbox);
        await IngestFocusBatchAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private async Task IngestFocusBatchAsync(ViewportHostContext context, CancellationToken cancellationToken)
    {
        if (censys.CanUseSearch)
        {
            await IngestViaSearchAsync(context.CountryCode, cancellationToken).ConfigureAwait(false);
        }

        await IngestViaCidrSamplingAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private async Task IngestViaSearchAsync(string countryCode, CancellationToken cancellationToken)
    {
        var state = await repository.GetOrCreateIngestStateAsync(countryCode, cancellationToken).ConfigureAwait(false);
        if (state.SearchComplete)
        {
            return;
        }

        var page = await censys
            .SearchCountryPageAsync(countryCode, state.SearchPageToken, cancellationToken)
            .ConfigureAwait(false);

        if (page is null)
        {
            return;
        }

        foreach (var record in page.Hosts)
        {
            if (await repository.ExistsAsync(record.Ip, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            await repository.UpsertAsync(new DiscoveredHostEntity
            {
                Ip = record.Ip,
                CountryCode = record.CountryCode ?? countryCode,
                Lat = record.Lat,
                Lng = record.Lng,
                City = record.City,
                Country = record.Country,
                Org = record.Org,
                Port = record.Port,
                Product = record.Product,
                Transport = record.Transport ?? "tcp",
                Source = "CensysSearch",
                CensysFetchedAt = DateTimeOffset.UtcNow,
                IsUp = record.Port is not null,
                VulnerabilitiesJson = HostVulnerabilityJson.Serialize(record.Vulnerabilities)
            }, cancellationToken).ConfigureAwait(false);
        }

        state.SearchPageToken = page.NextPageToken;
        state.SearchComplete = string.IsNullOrEmpty(page.NextPageToken);
        await repository.SaveIngestStateAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private async Task IngestViaCidrSamplingAsync(ViewportHostContext context, CancellationToken cancellationToken)
    {
        var blocks = await cidrProvider.GetCountryBlocksAsync(context.CountryCode, cancellationToken).ConfigureAwait(false);
        if (blocks.Count == 0)
        {
            return;
        }

        var batchSize = Math.Clamp(options.Value.CidrBatchSize, 4, 16);
        var cursorKey = CidrCursorCachePrefix + context.IngestKey;
        var cursor = cache.TryGetValue(cursorKey, out int cachedCursor) ? cachedCursor : 0;
        var random = Random.Shared;
        var saved = 0;
        const int maxSavePerRequest = 4;

        for (var i = 0; i < batchSize && saved < maxSavePerRequest; i++)
        {
            cursor = (cursor + 1) % blocks.Count;
            var ip = CidrSampler.SampleIp(blocks[cursor], random);

            if (await repository.ExistsAsync(ip, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var entity = await ProbeAndBuildEntityAsync(ip, context, cancellationToken).ConfigureAwait(false);
            if (entity is not null)
            {
                await repository.UpsertAsync(entity, cancellationToken).ConfigureAwait(false);
                saved++;
            }
        }

        cache.Set(cursorKey, cursor, TimeSpan.FromHours(24));
    }

    private async Task<DiscoveredHostEntity?> ProbeAndBuildEntityAsync(
        string ip,
        ViewportHostContext context,
        CancellationToken cancellationToken)
    {
        var dbPorts = await internetDb.GetOpenPortsAsync(ip, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<int> openPorts = dbPorts;
        if (openPorts.Count == 0)
        {
            openPorts = await portProbe.ScanAsync(ip, cancellationToken).ConfigureAwait(false);
        }

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

        return new DiscoveredHostEntity
        {
            Ip = ip,
            CountryCode = context.CountryCode,
            Lat = geo.Lat,
            Lng = geo.Lng,
            City = geo.City,
            Country = geo.Country,
            Org = geo.Org ?? geo.Isp,
            Port = openPorts[0],
            Product = PortProductMapper.Guess(openPorts),
            Transport = "tcp",
            Source = dbPorts.Count > 0 ? "InternetDb" : "Probe",
            IsUp = true,
            LastProbeAt = DateTimeOffset.UtcNow
        };
    }

    private async Task EnrichWithCensysLookupsAsync(ViewportHostContext context, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(options.Value.MaxCensysLookupsPerRequest, 0, 5);
        if (limit == 0)
        {
            return;
        }

        var focus = context.FocusBbox;
        var ips = await repository
            .ListIpsWithoutCensysInBboxAsync(
                focus.South,
                focus.North,
                focus.West,
                focus.East,
                limit,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var ip in ips)
        {
            var record = await censys.LookupHostAsync(ip, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                await repository.MarkCensysAttemptedAsync(ip, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await repository.UpsertAsync(new DiscoveredHostEntity
            {
                Ip = record.Ip,
                CountryCode = record.CountryCode ?? context.CountryCode,
                Lat = record.Lat,
                Lng = record.Lng,
                City = record.City,
                Country = record.Country,
                Org = record.Org,
                Port = record.Port,
                Product = record.Product,
                Transport = record.Transport ?? "tcp",
                Source = "Censys",
                CensysFetchedAt = DateTimeOffset.UtcNow,
                IsUp = record.Port is not null,
                VulnerabilitiesJson = HostVulnerabilityJson.Serialize(record.Vulnerabilities)
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RefreshReachabilityAsync(ViewportHostContext context, CancellationToken cancellationToken)
    {
        var probeBefore = DateTimeOffset.UtcNow.AddHours(-Math.Max(1, options.Value.ProbeTtlHours));
        var focus = context.FocusBbox;
        var stale = await repository
            .ListNeedingProbeAsync(
                focus.South,
                focus.North,
                focus.West,
                focus.East,
                probeBefore,
                6,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var host in stale)
        {
            var portsToCheck = host.Port is int knownPort
                ? new[] { knownPort }
                : new[] { 80, 443, 8080 };

            var open = await portProbe.ScanPortsAsync(host.Ip, portsToCheck, cancellationToken).ConfigureAwait(false);
            await repository.SaveProbeResultAsync(host.Ip, open.Count > 0, cancellationToken).ConfigureAwait(false);
        }
    }
}
