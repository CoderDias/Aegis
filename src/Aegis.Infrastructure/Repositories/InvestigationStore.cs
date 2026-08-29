using Aegis.Application.Abstractions;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Infrastructure.Repositories;

public sealed class InvestigationStore(AegisDbContext db) : IInvestigationStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<Investigation>> ListAsync(
        InvestigationStatus? status,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var query = db.Investigations
                .AsNoTracking()
                .Include(i => i.Assets)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(i => i.Status == status.Value);
            }

            var investigations = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            return investigations
                .OrderByDescending(i => i.UpdatedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Investigation?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await db.Investigations
                .AsNoTracking()
                .Include(i => i.Assets)
                .Include(i => i.Annotations)
                .Include(i => i.Timeline)
                .Include(i => i.Geofences)
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(Investigation investigation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(investigation);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DetachInvestigationGraph(investigation.Id);

            var tracked = await db.Investigations
                .Include(i => i.Assets)
                .Include(i => i.Annotations)
                .Include(i => i.Timeline)
                .Include(i => i.Geofences)
                .FirstOrDefaultAsync(i => i.Id == investigation.Id, cancellationToken)
                .ConfigureAwait(false);

            if (tracked is null)
            {
                db.Investigations.Add(investigation);
            }
            else
            {
                MergeInvestigation(tracked, investigation);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void MergeInvestigation(Investigation tracked, Investigation incoming)
    {
        db.Entry(tracked).CurrentValues.SetValues(incoming);

        // Geofences: add/update/remove
        foreach (var geofence in incoming.Geofences)
        {
            var existing = tracked.Geofences.FirstOrDefault(g => g.Id == geofence.Id);
            if (existing is null)
            {
                db.Geofences.Add(geofence);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(geofence);
            }
        }

        var removedGeofences = tracked.Geofences
            .Where(g => incoming.Geofences.All(ig => ig.Id != g.Id))
            .ToList();
        foreach (var removed in removedGeofences)
            db.Geofences.Remove(removed);

        // Timeline: append-only + read-state updates
        foreach (var timelineEvent in incoming.Timeline)
        {
            var existing = tracked.Timeline.FirstOrDefault(e => e.Id == timelineEvent.Id);
            if (existing is null)
            {
                db.TimelineEvents.Add(timelineEvent);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(timelineEvent);
            }
        }

        // Assets: add/update/remove
        foreach (var asset in incoming.Assets)
        {
            var existing = tracked.Assets.FirstOrDefault(a => a.Id == asset.Id);
            if (existing is null)
            {
                db.Assets.Add(asset);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(asset);
            }
        }

        var removedAssets = tracked.Assets
            .Where(a => incoming.Assets.All(ia => ia.Id != a.Id))
            .ToList();
        foreach (var removed in removedAssets)
            db.Assets.Remove(removed);

        // Annotations: add/update/remove
        foreach (var annotation in incoming.Annotations)
        {
            var existing = tracked.Annotations.FirstOrDefault(a => a.Id == annotation.Id);
            if (existing is null)
            {
                db.Annotations.Add(annotation);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(annotation);
            }
        }

        var removedAnnotations = tracked.Annotations
            .Where(a => incoming.Annotations.All(ia => ia.Id != a.Id))
            .ToList();
        foreach (var removed in removedAnnotations)
            db.Annotations.Remove(removed);
    }

    private void DetachInvestigationGraph(Guid investigationId)
    {
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            var detach = entry.Entity switch
            {
                Investigation i => i.Id == investigationId,
                Asset a => a.InvestigationId == investigationId,
                Annotation an => an.InvestigationId == investigationId,
                TimelineEvent t => t.InvestigationId == investigationId,
                Geofence g => g.InvestigationId == investigationId,
                _ => false
            };

            if (detach)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var investigation = await db.Investigations
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (investigation is null)
            {
                return;
            }

            db.Investigations.Remove(investigation);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
