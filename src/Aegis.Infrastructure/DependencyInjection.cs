using System.Threading.Channels;
using Aegis.Application.Abstractions;
using Aegis.Infrastructure.Flight;
using Aegis.Infrastructure.Geo;
using Aegis.Application.Dtos.Flights;
using Aegis.Infrastructure.Background;
using Aegis.Infrastructure.Cache;
using Aegis.Infrastructure.Data;
using Aegis.Infrastructure.Data.Seed;
using Aegis.Infrastructure.External.Adapters;
using Aegis.Infrastructure.External.Nominatim;
using Aegis.Infrastructure.External.AirStream;
using Aegis.Infrastructure.External.OpenSky;
using Aegis.Infrastructure.External.Overpass;
using Aegis.Infrastructure.External.Shodan;
using Aegis.Infrastructure.External.HostDiscovery;
using Aegis.Infrastructure.External.Censys;
using Aegis.Infrastructure.External.RansomwareLive;
using Aegis.Infrastructure.External.GeoIntel;
using Aegis.Infrastructure.External.OsintBrazuca;
using Aegis.Infrastructure.External.Brasil;
using Aegis.Infrastructure.External.Weather;
using Aegis.Infrastructure.Intel;
using Aegis.Infrastructure.Options;
using Aegis.Infrastructure.Repositories;
using Aegis.Infrastructure.Settings;
using Aegis.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Aegis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<FlightsOptions>(configuration.GetSection(FlightsOptions.SectionName));
        services.Configure<MapOptions>(configuration.GetSection(MapOptions.SectionName));
        services.Configure<OpenStreetMapOptions>(configuration.GetSection(OpenStreetMapOptions.SectionName));
        services.Configure<OpenSkyOptions>(configuration.GetSection(OpenSkyOptions.SectionName));
        services.Configure<NominatimOptions>(configuration.GetSection(NominatimOptions.SectionName));
        services.Configure<OverpassOptions>(configuration.GetSection(OverpassOptions.SectionName));
        services.Configure<AirStreamOptions>(configuration.GetSection(AirStreamOptions.SectionName));
        services.Configure<ShodanOptions>(configuration.GetSection(ShodanOptions.SectionName));
        services.Configure<HostDiscoveryOptions>(configuration.GetSection(HostDiscoveryOptions.SectionName));
        services.Configure<CensysOptions>(configuration.GetSection(CensysOptions.SectionName));
        services.Configure<RssOptions>(configuration.GetSection(RssOptions.SectionName));
        services.Configure<GeoIntelOptions>(configuration.GetSection(GeoIntelOptions.SectionName));
        services.Configure<RegionalPrefetchOptions>(configuration.GetSection(RegionalPrefetchOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=App_Data/aegis.db";

        services.AddDbContext<AegisDbContext>(options =>
            options.UseSqlite(connectionString)
                .AddInterceptors(new SqliteWalInterceptor()));

        services.AddMemoryCache();

        services.AddScoped<IInvestigationStore, InvestigationStore>();
        services.AddScoped<IFlightSnapshotStore, FlightSnapshotStore>();
        services.AddScoped<IFlightTrackingService, FlightTrackingService>();
        services.Configure<RepeaterBookOptions>(configuration.GetSection(RepeaterBookOptions.SectionName));
        services.AddSingleton<StaticGovernmentPoiCatalog>();
        services.AddSingleton<RepeaterBookCatalog>();
        services.AddSingleton<AnatelErbCatalog>();
        services.AddSingleton<PublicCameraCatalog>();
        services.AddSingleton<BrazilPortCatalog>();
        services.AddSingleton<IbgeMunicipalityCatalog>();
        services.AddSingleton<RepeaterBookClient>();
        services.AddHostedService<RepeaterBookRefreshService>();
        services.AddSingleton<IOsintBrazucaCatalog, OsintBrazucaCatalog>();
        services.AddSingleton<OsintBlockedUrlStore>();
        services.AddSingleton<IOsintSourceResolver, OsintSourceResolver>();
        services.AddSingleton<IOsintLinkHealthService, OsintLinkHealthService>();
        services.AddHostedService<OsintLinkHealthRefreshService>();
        services.AddHostedService<OsintStaticUrlPruneService>();
        services.AddSingleton<IBrasilApiClient, BrasilApiClient>();
        services.AddSingleton<IRdapBrClient, RdapBrClient>();
        services.AddSingleton<InmetAlertClient>();
        services.AddSingleton<DwdWeatherAlertClient>();
        services.AddSingleton<JmaWeatherAlertClient>();
        services.AddSingleton<RussiaWeatherAlertClient>();
        services.AddSingleton<WeatherAlertAggregator>();
        services.AddScoped<IMapFeatureService, MapFeatureService>();
        services.AddScoped<IGeocodingService, CachedGeocodingService>();
        services.AddSingleton<ShodanClient>();
        services.AddSingleton<CountryCidrProvider>();
        services.AddSingleton<InternetDbClient>();
        services.AddSingleton<TcpPortProbe>();
        services.AddSingleton<IpApiGeolocationClient>();
        services.AddSingleton<FreeHostDiscoveryClient>();
        services.AddSingleton<CensysQuotaService>();
        services.AddSingleton<CensysClient>();
        services.AddSingleton<DiscoveredHostRepository>();
        services.AddSingleton<CensysHostDiscoveryClient>();
        services.AddSingleton<IShodanDeviceService, CompositeHostDiscoveryService>();
        services.AddScoped<IRssFeedStore, RssFeedStore>();
        services.AddScoped<IIntegrationSettingsStore, IntegrationSettingsStore>();
        services.AddSingleton<IntegrationSettingsService>();

        services.AddSingleton<RegionalPrefetchRegistry>();
        services.AddSingleton<IRegionalPrefetchBroker>(sp => sp.GetRequiredService<RegionalPrefetchRegistry>());
        services.AddSingleton<OverpassCountryWarmer>();
        services.AddSingleton<CountryHostPrefetchIngestor>();
        services.AddHostedService<RegionalPrefetchService>();

        services.AddSingleton<IViewportBroker, ViewportBroker>();

        var flightChannel = Channel.CreateUnbounded<FlightSnapshot>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });
        services.AddSingleton(flightChannel);
        services.AddSingleton(flightChannel.Reader);
        services.AddSingleton(flightChannel.Writer);

        services.AddSingleton<OpenSkyClient>();
        services.AddSingleton<OpenSkyTokenProvider>();
        services.AddTransient<OpenSkyAuthHandler>();
        services.AddSingleton<AirStreamClient>();
        services.AddSingleton<NominatimClient>();
        services.AddScoped<AirportCoordinateProvider>();
        services.AddScoped<IFlightRouteResolver, FlightRouteResolver>();
        services.AddSingleton<OverpassClient>();

        services.AddScoped<NewsGeocodingService>();

        services.AddSingleton<IDataSourceAdapter, OpenSkyAdapter>();
        services.AddSingleton<IDataSourceAdapter, NominatimAdapter>();
        services.AddSingleton<IDataSourceAdapter, OverpassAdapter>();
        services.AddSingleton<IDataSourceAdapter, ShodanAdapter>();
        services.AddSingleton<IDataSourceAdapter, HostDiscoveryAdapter>();
        services.AddSingleton<IDataSourceAdapter, CensysAdapter>();
        services.AddSingleton<IDataSourceAdapter, AirStreamAdapter>();

        ConfigureHttpClients(services, configuration);

        services.AddHostedService<OpenSkyPollingService>();
        services.AddHostedService<RssPollingService>();
        services.AddSingleton<IRansomwareVictimCache, RansomwareVictimCache>();
        services.AddSingleton<RansomwareLiveClient>();
        services.AddHostedService<RansomwareLivePollingService>();
        services.AddSingleton<IGeoIntelCache, GeoIntelCache>();
        services.AddSingleton<UsgsEarthquakeClient>();
        services.AddSingleton<AisHubClient>();
        services.AddSingleton<OsmVesselFallbackClient>();
        services.AddHostedService<AisStreamIngestService>();
        services.AddHostedService<GeoIntelPollingService>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        var databaseOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        EnsureDatabaseDirectory(configuration);

        if (databaseOptions.MigrateOnStartup)
        {
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        if (databaseOptions.SeedDemo)
        {
            await DemoSeed.SeedAsync(db, cancellationToken).ConfigureAwait(false);
        }

        await IntegrationSettingsSeed.SeedAsync(db, configuration, cancellationToken).ConfigureAwait(false);

        var integrationSettings = scope.ServiceProvider.GetRequiredService<IntegrationSettingsService>();
        await integrationSettings.WarmCacheAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureDatabaseDirectory(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=App_Data/aegis.db";

        const string prefix = "Data Source=";
        var idx = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return;
        }

        var dataSource = connectionString[(idx + prefix.Length)..].Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var directory = Path.GetDirectoryName(dataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void ConfigureHttpClients(IServiceCollection services, IConfiguration configuration)
    {
        var openSky = configuration.GetSection(OpenSkyOptions.SectionName).Get<OpenSkyOptions>() ?? new OpenSkyOptions();
        var nominatim = configuration.GetSection(NominatimOptions.SectionName).Get<NominatimOptions>() ?? new NominatimOptions();
        var overpass = configuration.GetSection(OverpassOptions.SectionName).Get<OverpassOptions>() ?? new OverpassOptions();
        var airStream = configuration.GetSection(AirStreamOptions.SectionName).Get<AirStreamOptions>() ?? new AirStreamOptions();
        var shodan = configuration.GetSection(ShodanOptions.SectionName).Get<ShodanOptions>() ?? new ShodanOptions();

        services.AddHttpClient(HttpClientNames.OpenSky, client =>
            {
                client.BaseAddress = new Uri(openSky.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.Timeout = TimeSpan.FromSeconds(Math.Max(openSky.OnDemandTimeoutSeconds, 10));
            })
            .AddHttpMessageHandler<OpenSkyAuthHandler>()
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: true));

        services.AddHttpClient(HttpClientNames.Nominatim, client =>
            {
                client.BaseAddress = new Uri(nominatim.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(nominatim.UserAgent);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient(HttpClientNames.Overpass, client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(overpass.RequestTimeoutSeconds, 5, 30));
            });

        services.AddHttpClient(HttpClientNames.AirStream, client =>
            {
                client.BaseAddress = new Uri(airStream.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                if (!string.IsNullOrWhiteSpace(airStream.ApiToken))
                {
                    client.DefaultRequestHeaders.Add("api-auth", airStream.ApiToken);
                }

                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: true));

        services.AddHttpClient(HttpClientNames.Shodan, client =>
            {
                client.BaseAddress = new Uri("https://api.shodan.io/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: true));

        services.AddHttpClient("RepeaterBook", client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0 (local; contact: none)");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        services.AddHttpClient(HttpClientNames.RipeStat, client =>
            {
                client.BaseAddress = new Uri("https://stat.ripe.net/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient(HttpClientNames.IpDeny, client =>
            {
                client.BaseAddress = new Uri("https://www.ipdeny.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        // InternetDB: 404 é resposta normal (IP desconhecido) — sem retry nem telemetria Polly.
        services.AddHttpClient(HttpClientNames.InternetDb, client =>
            {
                client.BaseAddress = new Uri("https://internetdb.shodan.io/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.Timeout = TimeSpan.FromSeconds(8);
            });

        services.AddHttpClient(HttpClientNames.IpApi, client =>
            {
                client.BaseAddress = new Uri("http://ip-api.com/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient(HttpClientNames.Censys, client =>
            {
                client.BaseAddress = new Uri("https://api.platform.censys.io/v3/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.Timeout = TimeSpan.FromSeconds(20);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("RansomwareLive", client =>
            {
                client.BaseAddress = new Uri("https://api.ransomware.live/v2/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(20);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("GeoIntel", client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(25);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("BrasilApi", client =>
            {
                client.BaseAddress = new Uri("https://brasilapi.com.br/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("IbgeLocalidades", client =>
            {
                client.BaseAddress = new Uri("https://servicodados.ibge.gov.br/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("RdapBr", client =>
            {
                client.BaseAddress = new Uri("https://rdap.registro.br/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/rdap+json, application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("InmetAlerts", client =>
            {
                client.BaseAddress = new Uri("https://apiprevmet3.inmet.gov.br/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(20);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("DwdAlerts", client =>
            {
                client.BaseAddress = new Uri("https://www.dwd.de/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/javascript, */*");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(25);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("DwdGeo", client =>
            {
                client.BaseAddress = new Uri("https://maps.dwd.de/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(45);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("JmaAlerts", client =>
            {
                client.BaseAddress = new Uri("https://www.jma.go.jp/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(25);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("RoshydrometAlerts", client =>
            {
                client.BaseAddress = new Uri("https://mpr.meteoinfo.ru/");
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html, application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT/1.0");
                client.Timeout = TimeSpan.FromSeconds(25);
            })
            .AddStandardResilienceHandler(options => ConfigureResilience(options, useCircuitBreaker: false));

        services.AddHttpClient("OsintHealth", client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-OSINT-Health/1.0");
                client.Timeout = TimeSpan.FromSeconds(8);
            });
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions options, bool useCircuitBreaker)
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Result?.StatusCode is System.Net.HttpStatusCode.RequestTimeout or
                System.Net.HttpStatusCode.TooManyRequests ||
            args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError ||
            args.Outcome.Exception is HttpRequestException);

        if (useCircuitBreaker)
        {
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(60);
        }
        else
        {
            options.CircuitBreaker.ShouldHandle = _ => ValueTask.FromResult(false);
        }
    }
}
