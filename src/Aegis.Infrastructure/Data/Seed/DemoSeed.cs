using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Infrastructure.Data.Seed;

public static class DemoSeed
{
    private const string SeedFlagKey = "demo_seed_v1";

    public static async Task SeedAsync(AegisDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Investigations.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var seedTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        var alpha = Investigation.Create(
            "Operação Alpha",
            "Investigação demo em Brasília — palácio, ponto de interesse e área monitorada.",
            seedTime);

        var planaltoLocation = Coordinate.Create(-15.7997, -47.8645);
        var planaltoAsset = Asset.Create(
            Guid.NewGuid(),
            alpha.Id,
            AssetType.Building,
            "Palácio do Planalto",
            DataSourceType.Manual,
            "way|123456789",
            planaltoLocation,
            """{"osmType":"way","osmId":123456789,"amenity":"government","name":"Palácio do Planalto","address":"Praça dos Três Poderes, Brasília"}""",
            seedTime);
        alpha.AddAsset(planaltoAsset, seedTime);

        var meetingPoint = Coordinate.Create(-15.7801, -47.9292);
        var coordinateAsset = Asset.Create(
            Guid.NewGuid(),
            alpha.Id,
            AssetType.Coordinate,
            "Ponto de encontre — Lago Sul",
            DataSourceType.Manual,
            null,
            meetingPoint,
            """{"label":"Ponto de encontro","accuracyMeters":15}""",
            seedTime);
        alpha.AddAsset(coordinateAsset, seedTime);

        var pin = Annotation.Create(
            Guid.NewGuid(),
            alpha.Id,
            AnnotationKind.Pin,
            """{"type":"Point","coordinates":[-47.8645,-15.7997]}""",
            seedTime,
            "Entrada principal",
            Annotation.DefaultColor);
        alpha.AddAnnotation(pin, seedTime);

        var polygon = Annotation.Create(
            Guid.NewGuid(),
            alpha.Id,
            AnnotationKind.Polygon,
            """{"type":"Polygon","coordinates":[[[-47.8700,-15.8050],[-47.8580,-15.8050],[-47.8580,-15.7950],[-47.8700,-15.7950],[-47.8700,-15.8050]]]}""",
            seedTime,
            "Área de interesse — Esplanada",
            "#1f6feb");
        alpha.AddAnnotation(polygon, seedTime);

        alpha.AddNote("Reconhecimento inicial concluído — aguardando correlação aérea.", seedTime.AddMinutes(30));
        alpha.AddNote("Geofence de 5 km ativada ao redor do Planalto.", seedTime.AddHours(1));

        var geofence = Geofence.Create(
            Guid.NewGuid(),
            alpha.Id,
            "Perímetro Planalto 5km",
            """{"type":"Circle","center":[-47.8645,-15.7997],"radiusMeters":5000}""",
            seedTime);
        alpha.AddGeofence(geofence, seedTime);

        var corridor = Investigation.Create(
            "Corredor Aéreo — Demo",
            "Caso arquivado com aeronave fictícia — tracks reais virão do OpenSky.",
            seedTime.AddDays(-7));

        var aircraft = Asset.Create(
            Guid.NewGuid(),
            corridor.Id,
            AssetType.Aircraft,
            "DEMO01 (fictício)",
            DataSourceType.OpenSky,
            "abcdef",
            Coordinate.Create(-15.8711, -47.9186),
            """{"icao24":"abcdef","callsign":"DEMO01","originCountry":"Brazil","lastHeading":90.0,"note":"Tracks reais serão ingeridos pelo OpenSkyPollingService."}""",
            seedTime.AddDays(-7),
            "Asset demo — substituir por aeronave real da viewport.");
        corridor.AddAsset(aircraft, seedTime.AddDays(-7));

        corridor.ChangeStatus(InvestigationStatus.Archived, seedTime.AddDays(-1));

        db.Investigations.AddRange(alpha, corridor);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
