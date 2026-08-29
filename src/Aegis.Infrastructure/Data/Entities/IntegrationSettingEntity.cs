namespace Aegis.Infrastructure.Data.Entities;

public sealed class IntegrationSettingEntity
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public int SortOrder { get; set; }
}
