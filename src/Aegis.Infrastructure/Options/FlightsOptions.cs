namespace Aegis.Infrastructure.Options;

public sealed class FlightsOptions
{
    public const string SectionName = "Flights";

    public int RetentionDays { get; set; } = 7;

    public int MaxMarkers { get; set; } = 3000;
}
