using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.OsintBrazuca;

public sealed class OsintBlockedUrlStore(
    ILogger<OsintBlockedUrlStore> logger)
{
    private const string BlockedFileName = "blocked-urls.json";
    private readonly object _lock = new();
    private HashSet<string>? _blocked;

    public IReadOnlySet<string> GetBlockedUrls()
    {
        lock (_lock)
        {
            _blocked ??= Load();
            return _blocked;
        }
    }

    public bool IsBlocked(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return GetBlockedUrls().Contains(Normalize(url));
    }

    public void Block(string url, int statusCode)
    {
        if (statusCode != 404 || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var normalized = Normalize(url);
        lock (_lock)
        {
            _blocked ??= Load();
            if (!_blocked.Add(normalized))
            {
                return;
            }

            Save(_blocked);
            logger.LogInformation("OSINT URL bloqueada (404): {Url}", normalized);
        }
    }

    private HashSet<string> Load()
    {
        var path = GetPath();
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(path);
            var urls = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao ler {Path}", path);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save(HashSet<string> blocked)
    {
        var path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(blocked.OrderBy(u => u).ToList(), JsonOptions);
        File.WriteAllText(path, json);
    }

    private string GetPath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "osint-brazuca",
            BlockedFileName);

    private static string Normalize(string url) => url.Trim();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
