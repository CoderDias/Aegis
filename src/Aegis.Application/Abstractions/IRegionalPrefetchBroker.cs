namespace Aegis.Application.Abstractions;

public enum RegionalPrefetchPhase
{
    Queued,
    Warming,
    Warm,
    Refreshing
}

public sealed record RegionalPrefetchStatusDto(
    string CountryCode,
    RegionalPrefetchPhase Phase,
    int OverpassTilesCached,
    int OverpassTilesTotal,
    int HostsDiscovered,
    DateTimeOffset? LastBatchUtc);

public sealed record RegionalPrefetchSummaryDto(
    string? ActiveCountryCode,
    RegionalPrefetchPhase ActivePhase,
    int CountriesWarm,
    int CountriesWarming,
    int CountriesTotal,
    int OverpassTilesCached,
    int OverpassTilesTotal,
    int HostsDiscovered,
    double OverallProgress);

public interface IRegionalPrefetchBroker
{
    event Action<string>? CountryUpdated;

    string? ActiveCountryCode { get; }

    void SetActiveCountry(string? countryCode);

    RegionalPrefetchStatusDto GetStatus(string countryCode);

    IReadOnlyList<RegionalPrefetchStatusDto> GetAllStatuses();

    RegionalPrefetchSummaryDto GetSummary();

    void NotifyCountryUpdated(string countryCode);
}
