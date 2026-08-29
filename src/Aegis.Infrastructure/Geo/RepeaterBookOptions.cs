namespace Aegis.Infrastructure.Geo;

public sealed class RepeaterBookOptions
{
    public const string SectionName = "RepeaterBook";

    public bool Enabled { get; set; } = true;

    public string StateId { get; set; } = "BR";

    public int RefreshHours { get; set; } = 168;
}
