namespace Aegis.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool MigrateOnStartup { get; set; } = true;

    public bool SeedDemo { get; set; }
}
