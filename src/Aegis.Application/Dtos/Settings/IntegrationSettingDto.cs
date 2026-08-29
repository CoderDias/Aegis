namespace Aegis.Application.Dtos.Settings;

public record IntegrationSettingDto(
    string Key,
    string DisplayName,
    bool Enabled,
    bool IsConfigured,
    int SortOrder);
