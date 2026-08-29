using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Osint;
using Aegis.Application.Osint;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.Brasil;

public sealed class BrasilApiClient(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<BrasilApiClient> logger) : IBrasilApiClient
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    public async Task<CnpjLookupDto?> GetCnpjAsync(string cnpj, CancellationToken cancellationToken = default)
    {
        var normalized = OsintContextExtractor.NormalizeCnpj(cnpj);
        if (normalized is null)
        {
            return null;
        }

        if (cache.TryGetValue<CnpjLookupDto>(CacheKey(normalized), out var cached))
        {
            return cached;
        }

        try
        {
            var client = httpClientFactory.CreateClient("BrasilApi");
            using var response = await client.GetAsync($"api/cnpj/v1/{normalized}", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("BrasilAPI CNPJ {Cnpj} retornou {Status}", normalized, response.StatusCode);
                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<BrasilApiCnpjResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (payload is null)
            {
                return null;
            }

            var dto = new CnpjLookupDto(
                normalized,
                payload.RazaoSocial ?? normalized,
                payload.NomeFantasia,
                payload.DescricaoSituacaoCadastral,
                payload.Logradouro,
                payload.Numero,
                payload.Bairro,
                payload.Municipio,
                payload.Uf,
                payload.Cep,
                null,
                null);

            if (!string.IsNullOrWhiteSpace(payload.Cep))
            {
                var cep = await GetCepAsync(payload.Cep, cancellationToken).ConfigureAwait(false);
                if (cep?.Lat is not null && cep.Lng is not null)
                {
                    dto = dto with { Lat = cep.Lat, Lng = cep.Lng };
                }
            }

            cache.Set(CacheKey(normalized), dto, CacheDuration);
            return dto;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Falha ao consultar CNPJ {Cnpj} na BrasilAPI.", normalized);
            return null;
        }
    }

    public async Task<CepLookupDto?> GetCepAsync(string cep, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCep(cep);
        if (normalized is null)
        {
            return null;
        }

        if (cache.TryGetValue<CepLookupDto>(CepCacheKey(normalized), out var cached))
        {
            return cached;
        }

        try
        {
            var client = httpClientFactory.CreateClient("BrasilApi");
            using var response = await client.GetAsync($"api/cep/v2/{normalized}", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("BrasilAPI CEP {Cep} retornou {Status}", normalized, response.StatusCode);
                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<BrasilApiCepResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (payload is null || string.IsNullOrWhiteSpace(payload.Street))
            {
                return null;
            }

            var dto = new CepLookupDto(
                normalized,
                payload.Street,
                payload.Complement,
                payload.Neighborhood ?? "—",
                payload.City ?? "—",
                payload.State ?? "—",
                payload.Ibge,
                payload.Location?.Coordinates?.LatitudeValue,
                payload.Location?.Coordinates?.LongitudeValue);

            cache.Set(CepCacheKey(normalized), dto, CacheDuration);
            return dto;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Falha ao consultar CEP {Cep} na BrasilAPI.", normalized);
            return null;
        }
    }

    private static string? NormalizeCep(string cep)
    {
        if (string.IsNullOrWhiteSpace(cep))
        {
            return null;
        }

        var digits = new string(cep.Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? digits : null;
    }

    private static string CacheKey(string cnpj) => $"brasilapi:cnpj:{cnpj}";

    private static string CepCacheKey(string cep) => $"brasilapi:cep:{cep}";

    private sealed class BrasilApiCnpjResponse
    {
        [JsonPropertyName("razao_social")]
        public string? RazaoSocial { get; set; }

        [JsonPropertyName("nome_fantasia")]
        public string? NomeFantasia { get; set; }

        [JsonPropertyName("descricao_situacao_cadastral")]
        public string? DescricaoSituacaoCadastral { get; set; }

        [JsonPropertyName("logradouro")]
        public string? Logradouro { get; set; }

        [JsonPropertyName("numero")]
        public string? Numero { get; set; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; set; }

        [JsonPropertyName("municipio")]
        public string? Municipio { get; set; }

        [JsonPropertyName("uf")]
        public string? Uf { get; set; }

        [JsonPropertyName("cep")]
        public string? Cep { get; set; }
    }

    private sealed class BrasilApiCepResponse
    {
        [JsonPropertyName("street")]
        public string? Street { get; set; }

        [JsonPropertyName("complement")]
        public string? Complement { get; set; }

        [JsonPropertyName("neighborhood")]
        public string? Neighborhood { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("ibge")]
        public string? Ibge { get; set; }

        [JsonPropertyName("location")]
        public BrasilApiLocationResponse? Location { get; set; }
    }

    private sealed class BrasilApiLocationResponse
    {
        [JsonPropertyName("coordinates")]
        public BrasilApiCoordinatesResponse? Coordinates { get; set; }
    }

    private sealed class BrasilApiCoordinatesResponse
    {
        [JsonPropertyName("latitude")]
        public string? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public string? Longitude { get; set; }

        public double? LatitudeValue =>
            double.TryParse(Latitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat)
                ? lat
                : null;

        public double? LongitudeValue =>
            double.TryParse(Longitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lng)
                ? lng
                : null;
    }
}
