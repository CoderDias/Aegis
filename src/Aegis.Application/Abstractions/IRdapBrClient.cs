using Aegis.Application.Dtos.Osint;

namespace Aegis.Application.Abstractions;

public interface IRdapBrClient
{
    Task<RdapDomainDto?> GetDomainAsync(string domain, CancellationToken cancellationToken = default);
}
