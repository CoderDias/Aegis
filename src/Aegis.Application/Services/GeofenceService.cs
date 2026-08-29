using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Investigations;
using Aegis.Application.Geo;
using Aegis.Application.Mapping;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;

namespace Aegis.Application.Services;

public sealed class GeofenceService(IInvestigationStore store, IClock clock)
{
    public async Task<GeofenceDto> AddFromDrawAsync(
        Guid investigationId,
        string name,
        string drawKind,
        string drawGeometryJson,
        CancellationToken cancellationToken = default)
    {
        var geofenceGeometry = GeofenceGeometryMapper.ToGeofenceGeometry(drawKind, drawGeometryJson)
            ?? throw new DomainException("Desenhe um polígono ou círculo para definir a área monitorada.");

        return await AddAsync(investigationId, name, geofenceGeometry, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GeofenceDto> AddAsync(
        Guid investigationId,
        string name,
        string geofenceGeometryJson,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        var geofence = Geofence.Create(
            Guid.NewGuid(),
            investigationId,
            name.Trim(),
            geofenceGeometryJson,
            clock.UtcNow,
            isEnabled: true);

        investigation.AddGeofence(geofence, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return geofence.ToDto();
    }

    public async Task ToggleAsync(
        Guid investigationId,
        Guid geofenceId,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        investigation.ToggleGeofence(geofenceId, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(
        Guid investigationId,
        Guid geofenceId,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        investigation.RemoveGeofence(geofenceId, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Investigation> GetMutableInvestigationAsync(Guid id, CancellationToken cancellationToken)
    {
        var investigation = await store.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Investigation {id} not found.");

        if (investigation.Status == InvestigationStatus.Archived)
        {
            throw new DomainException("Archived investigations are read-only.");
        }

        return investigation;
    }
}
