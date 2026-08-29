using Aegis.Application.Settings;
using Aegis.Infrastructure.Data.Entities;
using Aegis.Infrastructure.External.AirStream;
using Aegis.Infrastructure.External.Censys;
using Aegis.Infrastructure.External.OpenSky;
using Aegis.Infrastructure.External.Shodan;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Aegis.Infrastructure.Data.Seed;

public static class IntegrationSettingsSeed
{
    public static async Task SeedAsync(AegisDbContext db, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (await db.IntegrationSettings.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var openSky = configuration.GetSection(OpenSkyOptions.SectionName).Get<OpenSkyOptions>() ?? new OpenSkyOptions();
        var airStream = configuration.GetSection(AirStreamOptions.SectionName).Get<AirStreamOptions>() ?? new AirStreamOptions();
        var shodan = configuration.GetSection(ShodanOptions.SectionName).Get<ShodanOptions>() ?? new ShodanOptions();
        var censys = configuration.GetSection(CensysOptions.SectionName).Get<CensysOptions>() ?? new CensysOptions();
        var hostDiscoveryEnabled = configuration.GetValue("HostDiscovery:Enabled", true);

        var rows = new[]
        {
            Create(IntegrationKeys.OpenSky, "OpenSky Network", true, 10),
            Create(IntegrationKeys.AirStream, "AirStream (adsb.fi)", airStream.Enabled, 20),
            Create(IntegrationKeys.Shodan, "Shodan", shodan.Enabled, 30),
            Create(IntegrationKeys.Censys, "Censys", censys.Enabled, 40),
            Create(IntegrationKeys.HostDiscovery, "Host discovery gratuito", hostDiscoveryEnabled, 50),
            Create(IntegrationKeys.Rss, "Feeds RSS", true, 60)
        };

        db.IntegrationSettings.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IntegrationSettingEntity Create(string key, string displayName, bool enabled, int sortOrder) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            Enabled = enabled,
            SortOrder = sortOrder
        };
}
