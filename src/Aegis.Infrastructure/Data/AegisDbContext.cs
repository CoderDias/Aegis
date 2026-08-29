using Aegis.Domain.Entities;
using Aegis.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class AegisDbContext(DbContextOptions<AegisDbContext> options) : DbContext(options)
{
    public DbSet<Investigation> Investigations => Set<Investigation>();

    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<Annotation> Annotations => Set<Annotation>();

    public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();

    public DbSet<Geofence> Geofences => Set<Geofence>();

    public DbSet<FlightTrackPoint> FlightTrackPoints => Set<FlightTrackPoint>();

    public DbSet<GeocodeCacheEntry> GeocodeCache => Set<GeocodeCacheEntry>();

    public DbSet<RssFeedEntity> RssFeeds => Set<RssFeedEntity>();

    public DbSet<NewsItemEntity> NewsItems => Set<NewsItemEntity>();

    public DbSet<DiscoveredHostEntity> DiscoveredHosts => Set<DiscoveredHostEntity>();

    public DbSet<CensysApiUsageEntity> CensysApiUsage => Set<CensysApiUsageEntity>();

    public DbSet<CountryIngestStateEntity> CountryIngestStates => Set<CountryIngestStateEntity>();

    public DbSet<IntegrationSettingEntity> IntegrationSettings => Set<IntegrationSettingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AegisDbContext).Assembly);
    }
}
