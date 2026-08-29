namespace Aegis.Application.Dtos.Intel;

public sealed record ShodanApiInfoDto(
    string Plan,
    int QueryCredits,
    int ScanCredits,
    bool SearchAvailable);
