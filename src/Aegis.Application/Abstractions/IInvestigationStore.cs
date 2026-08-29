using Aegis.Domain.Entities;
using Aegis.Domain.Enums;

namespace Aegis.Application.Abstractions;

public interface IInvestigationStore
{
    Task<IReadOnlyList<Investigation>> ListAsync(InvestigationStatus? status, CancellationToken cancellationToken = default);

    Task<Investigation?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Investigation investigation, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
