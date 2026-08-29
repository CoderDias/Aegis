namespace Aegis.Infrastructure.Options;

public sealed class OpenStreetMapOptions
{
    public const string SectionName = "OpenStreetMap";

    public string? StyleUrl { get; set; } =
        "https://tiles.openfreemap.org/styles/dark";

    public string TileUrl { get; set; } =
        "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";

    public string FallbackTileUrl { get; set; } =
        "https://{s}.tile.openstreetmap.de/{z}/{x}/{y}.png";

    public string Attribution { get; set; } =
        "&copy; OpenStreetMap contributors &copy; OpenFreeMap";
}
