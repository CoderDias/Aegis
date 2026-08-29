using System.Text.Json;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Osint;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.OsintBrazuca;

public sealed class OsintBrazucaCatalog : IOsintBrazucaCatalog
{
    private readonly IReadOnlyList<OsintSourceDto> _sources;
    private readonly OsintBlockedUrlStore _blockedStore;
    private readonly IReadOnlyList<string> _categories;
    private readonly IReadOnlyList<string> _inputTypes;

    public OsintBrazucaCatalog(
        IOptions<OsintBrazucaOptions> options,
        OsintBlockedUrlStore blockedStore,
        ILogger<OsintBrazucaCatalog> logger)
    {
        _blockedStore = blockedStore;
        _sources = LoadSources(options.Value, logger);
        _categories = VisibleSources()
            .Select(s => s.Categoria)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _inputTypes = VisibleSources()
            .SelectMany(s => s.Input)
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(i => i, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public int TotalCount => VisibleSources().Count();

    public IReadOnlyList<OsintSourceDto> GetAllSources() => VisibleSources().ToList();

    private IEnumerable<OsintSourceDto> VisibleSources() =>
        _sources.Where(s => !_blockedStore.IsBlocked(s.Url));

    public IReadOnlyList<string> GetCategories() => _categories;

    public IReadOnlyList<string> GetInputTypes() => _inputTypes;

    public OsintSourceDto? GetById(string fonteId) =>
        VisibleSources().FirstOrDefault(s => s.FonteId.Equals(fonteId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<OsintSourceDto> Search(OsintSearchQuery query)
    {
        IEnumerable<OsintSourceDto> results = VisibleSources();

        if (!string.IsNullOrWhiteSpace(query.Categoria))
        {
            results = results.Where(s =>
                s.Categoria.Equals(query.Categoria, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.CategoriaId))
        {
            results = results.Where(s =>
                s.CategoriaId.Equals(query.CategoriaId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.InputType))
        {
            results = results.Where(s =>
                s.Input.Any(i => i.Equals(query.InputType, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.OutputType))
        {
            results = results.Where(s =>
                s.Output.Any(o => o.Equals(query.OutputType, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Uf))
        {
            results = results.Where(s =>
                s.Uf is null ||
                s.Uf.Equals(query.Uf, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var terms = query.Text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            results = results.Where(s => terms.All(term => MatchesTerm(s, term)));
        }

        return results
            .Take(Math.Clamp(query.Limit, 1, 200))
            .ToList();
    }

    private static bool MatchesTerm(OsintSourceDto source, string term)
    {
        return Contains(source.Fonte, term) ||
               Contains(source.Categoria, term) ||
               Contains(source.Descricao, term) ||
               Contains(source.Url, term) ||
               source.Input.Any(i => Contains(i, term)) ||
               source.Output.Any(o => Contains(o, term));
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;

    private static IReadOnlyList<OsintSourceDto> LoadSources(
        OsintBrazucaOptions options,
        ILogger<OsintBrazucaCatalog> logger)
    {
        try
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "osint-brazuca",
                options.IndexFileName);

            if (!File.Exists(path))
            {
                logger.LogWarning("Catálogo OSINT Brazuca não encontrado em {Path}", path);
                return [];
            }

            var json = File.ReadAllText(path);
            var root = JsonSerializer.Deserialize<IndexRoot>(json, JsonOptions);
            if (root?.Links is null || root.Links.Count == 0)
            {
                logger.LogWarning("Catálogo OSINT Brazuca vazio ou inválido.");
                return [];
            }

            return root.Links
                .Where(link => !string.IsNullOrWhiteSpace(link.Url))
                .Select(ToDto)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao carregar catálogo OSINT Brazuca.");
            return [];
        }
    }

    private static OsintSourceDto ToDto(OsintLinkEntry link) =>
        new(
            link.FonteId,
            string.IsNullOrWhiteSpace(link.Fonte) ? link.Url : link.Fonte,
            link.Categoria,
            link.CategoriaId,
            link.Url,
            string.IsNullOrWhiteSpace(link.Descricao) ? null : link.Descricao,
            link.TipoFonte,
            link.Uf,
            link.Input,
            link.Output);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class IndexRoot
    {
        public List<OsintLinkEntry> Links { get; set; } = [];
    }

    private sealed class OsintLinkEntry
    {
        public string Categoria { get; set; } = string.Empty;
        public string CategoriaId { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Fonte { get; set; } = string.Empty;
        public string FonteId { get; set; } = string.Empty;
        public List<string> Input { get; set; } = [];
        public List<string> Output { get; set; } = [];
        public string? TipoFonte { get; set; }
        public string? Uf { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}
