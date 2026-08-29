using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Investigations;
using Aegis.Application.Mapping;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;

namespace Aegis.Application.Services;

public sealed class AnnotationService(IInvestigationStore store, IClock clock)
{
    public async Task<IReadOnlyList<AnnotationDto>> ListAsync(
        Guid investigationId,
        CancellationToken cancellationToken = default)
    {
        var investigation = await store.GetAsync(investigationId, cancellationToken).ConfigureAwait(false);
        return investigation?.Annotations.Select(a => a.ToDto()).ToList() ?? [];
    }

    public async Task<AnnotationDto?> GetAsync(
        Guid investigationId,
        Guid annotationId,
        CancellationToken cancellationToken = default)
    {
        var investigation = await store.GetAsync(investigationId, cancellationToken).ConfigureAwait(false);
        return investigation?.Annotations.FirstOrDefault(a => a.Id == annotationId)?.ToDto();
    }

    public async Task<AnnotationDto> AddFromDrawAsync(
        Guid investigationId,
        AnnotationKind kind,
        string geometryJson,
        string? label,
        string color,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        var annotation = Annotation.Create(
            Guid.NewGuid(),
            investigationId,
            kind,
            geometryJson,
            clock.UtcNow,
            label,
            color);

        investigation.AddAnnotation(annotation, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return annotation.ToDto();
    }

    public async Task<AnnotationDto> UpdateAsync(
        Guid investigationId,
        Guid annotationId,
        string? label,
        string color,
        string geometryJson,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        investigation.UpdateAnnotationGeometry(annotationId, label, color, geometryJson, clock.UtcNow);
        var annotation = investigation.Annotations.First(a => a.Id == annotationId);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return annotation.ToDto();
    }

    public async Task DeleteAsync(
        Guid investigationId,
        Guid annotationId,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        investigation.RemoveAnnotation(annotationId, clock.UtcNow);
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
