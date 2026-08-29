namespace Aegis.Infrastructure.Options;

public sealed class MapOptions
{
    public const string SectionName = "Map";

    public double DefaultLat { get; set; } = -15.7934;

    public double DefaultLng { get; set; } = -47.8822;

    public int DefaultZoom { get; set; } = 5;

    public int MaxZoom { get; set; } = 20;

    public int MinZoom { get; set; } = 2;
}
