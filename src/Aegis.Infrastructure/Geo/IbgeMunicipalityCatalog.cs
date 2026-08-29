using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Geo;

public sealed class IbgeMunicipalityCatalog
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<IbgeMunicipalityCatalog> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public IbgeMunicipalityCatalog(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<IbgeMunicipalityCatalog> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IbgeMunicipality?> ResolveAsync(
        string? cityName,
        string? uf,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return null;
        }

        var municipalities = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var normalizedCity = NormalizeName(cityName);
        var normalizedUf = string.IsNullOrWhiteSpace(uf) ? null : uf.Trim().ToUpperInvariant();

        return municipalities.FirstOrDefault(m =>
            (normalizedUf is null || string.Equals(m.Uf, normalizedUf, StringComparison.OrdinalIgnoreCase)) &&
            (NormalizeName(m.Name) == normalizedCity ||
             NormalizeName(m.Name).Contains(normalizedCity, StringComparison.Ordinal) ||
             normalizedCity.Contains(NormalizeName(m.Name), StringComparison.Ordinal)));
    }

    public async Task<IbgeMunicipality?> GetByIbgeCodeAsync(
        string? ibgeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ibgeCode))
        {
            return null;
        }

        var municipalities = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return municipalities.FirstOrDefault(m =>
            string.Equals(m.IbgeCode, ibgeCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<string>> GetUfsAsync(CancellationToken cancellationToken = default)
    {
        var municipalities = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return municipalities
            .Select(m => m.Uf)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string FormatBrazilLabel(string? city, string? uf, string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(uf))
        {
            return detail ?? "Brasil";
        }

        var core = string.IsNullOrWhiteSpace(uf) ? city ?? "Brasil" : $"{city}/{uf}";
        return string.IsNullOrWhiteSpace(detail) ? core : $"{core} — {detail}";
    }

    private async Task<IReadOnlyList<IbgeMunicipality>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<IReadOnlyList<IbgeMunicipality>>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue<IReadOnlyList<IbgeMunicipality>>(CacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var client = _httpClientFactory.CreateClient("IbgeLocalidades");
            var payload = await client
                .GetFromJsonAsync<List<BrasilApiMunicipalityResponse>>(
                    "api/v1/localidades/municipios",
                    cancellationToken)
                .ConfigureAwait(false) ?? [];

            var municipalities = payload
                .Select(m => new IbgeMunicipality(
                    m.Id.ToString(CultureInfo.InvariantCulture),
                    m.Nome ?? string.Empty,
                    m.Microrregiao?.Mesorregiao?.Uf?.Sigla ?? string.Empty))
                .Where(m => !string.IsNullOrWhiteSpace(m.Name) && !string.IsNullOrWhiteSpace(m.Uf))
                .ToList();

            _cache.Set(CacheKey, municipalities, TimeSpan.FromDays(7));
            return municipalities;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao carregar municípios IBGE.");
            return [];
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static string NormalizeName(string value)
    {
        var formD = value.Trim().ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = formD.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray()).Normalize(System.Text.NormalizationForm.FormC);
    }

    private const string CacheKey = "ibge:municipalities:v1";

    private sealed class BrasilApiMunicipalityResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("microrregiao")]
        public MicrorregiaoResponse? Microrregiao { get; set; }
    }

    private sealed class MicrorregiaoResponse
    {
        [JsonPropertyName("mesorregiao")]
        public MesorregiaoResponse? Mesorregiao { get; set; }
    }

    private sealed class MesorregiaoResponse
    {
        [JsonPropertyName("UF")]
        public UfResponse? Uf { get; set; }
    }

    private sealed class UfResponse
    {
        [JsonPropertyName("sigla")]
        public string? Sigla { get; set; }
    }
}

public sealed record IbgeMunicipality(string IbgeCode, string Name, string Uf);
