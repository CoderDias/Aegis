namespace Aegis.Application.Dtos.Osint;

public record RdapDomainDto(
    string Domain,
    string? Status,
    IReadOnlyList<string> Nameservers,
    DateTimeOffset? RegistrationDate,
    DateTimeOffset? ExpirationDate,
    string? RegistrantHandle);
