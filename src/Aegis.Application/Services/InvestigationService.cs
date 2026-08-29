using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Investigations;
using Aegis.Application.Geo;
using Aegis.Application.Mapping;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;

namespace Aegis.Application.Services;

public sealed class InvestigationService(
    IInvestigationStore store,
    IClock clock)
{
    public async Task<IReadOnlyList<InvestigationSummaryDto>> ListAsync(
        InvestigationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var investigations = await store.ListAsync(status, cancellationToken).ConfigureAwait(false);
        return investigations
            .Select(i => i.ToSummaryDto())
            .OrderByDescending(i => i.UpdatedAt)
            .ToList();
    }

    public async Task<InvestigationDetailDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var investigation = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return investigation?.ToDetailDto();
    }

    public async Task<InvestigationDetailDto> CreateAsync(
        string title,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var investigation = Investigation.Create(title, description, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return investigation.ToDetailDto();
    }

    public async Task<InvestigationDetailDto> CreateWithGeofenceAsync(
        string title,
        string? description,
        string drawKind,
        string drawGeometryJson,
        string geofenceName = "Área monitorada",
        CancellationToken cancellationToken = default)
    {
        var geofenceGeometry = GeofenceGeometryMapper.ToGeofenceGeometry(drawKind, drawGeometryJson)
            ?? throw new DomainException("Desenhe um polígono ou círculo para definir a área monitorada.");

        var investigation = Investigation.Create(title, description, clock.UtcNow);
        var geofence = Geofence.Create(
            Guid.NewGuid(),
            investigation.Id,
            geofenceName.Trim(),
            geofenceGeometry,
            clock.UtcNow,
            isEnabled: true);
        investigation.AddGeofence(geofence, clock.UtcNow);

        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return investigation.ToDetailDto();
    }

    public async Task<InvestigationDetailDto> UpdateAsync(
        Guid id,
        string title,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetRequiredAsync(id, cancellationToken).ConfigureAwait(false);
        investigation.UpdateDetails(title, description, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return investigation.ToDetailDto();
    }

    public async Task<InvestigationDetailDto> ChangeStatusAsync(
        Guid id,
        InvestigationStatus status,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetRequiredAsync(id, cancellationToken).ConfigureAwait(false);
        investigation.ChangeStatus(status, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return investigation.ToDetailDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TimelineEventDto>> GetAlertsAsync(
        Guid investigationId,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetRequiredAsync(investigationId, cancellationToken).ConfigureAwait(false);
        return investigation.Timeline
            .Where(e => e.Type == TimelineEventType.GeofenceAlert)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => e.ToDto())
            .ToList();
    }

    public async Task MarkAlertReadAsync(
        Guid investigationId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetRequiredAsync(investigationId, cancellationToken).ConfigureAwait(false);
        investigation.MarkAlertRead(eventId, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkAllAlertsReadAsync(
        Guid investigationId,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetRequiredAsync(investigationId, cancellationToken).ConfigureAwait(false);
        investigation.MarkAllAlertsRead(clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordOsintSourceAccessAsync(
        Guid investigationId,
        string fonteName,
        string url,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetRequiredAsync(investigationId, cancellationToken).ConfigureAwait(false);
        investigation.RecordOsintSourceAccess(fonteName, url, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddNoteAsync(
        Guid investigationId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetRequiredAsync(investigationId, cancellationToken).ConfigureAwait(false);
        investigation.AddNote(message, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Valida que a investigação existe e pode ser usada como ativa na UI.
    /// A persistência do id ativo fica na camada Web (ProtectedLocalStorage).
    /// </summary>
    public Task ValidateActiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetActiveAsync(id, cancellationToken);

    public async Task SetActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var investigation = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (investigation is null)
        {
            throw new InvalidOperationException($"Investigation {id} not found.");
        }

        if (investigation.Status == InvestigationStatus.Archived)
        {
            throw new DomainException("Archived investigations cannot be set as active.");
        }
    }

    private async Task<Investigation> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var investigation = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return investigation ?? throw new InvalidOperationException($"Investigation {id} not found.");
    }
}
