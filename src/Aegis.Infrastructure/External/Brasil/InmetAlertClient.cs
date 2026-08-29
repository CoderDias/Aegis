using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aegis.Application.Dtos.Intel;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.Brasil;

public sealed class InmetAlertClient(
    IHttpClientFactory httpClientFactory,
    ILogger<InmetAlertClient> logger)
{
    public async Task<IReadOnlyList<GeoMarkerDto>> FetchActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("InmetAlerts");
            using var response = await client.GetAsync("avisos/ativos", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("INMET avisos retornou {Status}", response.StatusCode);
                return [];
            }

            var payload = await response.Content
                .ReadFromJsonAsync<InmetAlertResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (payload is null)
            {
                return [];
            }

            var markers = new List<GeoMarkerDto>();
            foreach (var alert in payload.Hoje ?? [])
            {
                var centroid = ParseCentroid(alert.Poligono);
                if (centroid is null)
                {
                    continue;
                }

                var title = alert.Descricao ?? alert.Titulo ?? "Aviso meteorológico INMET";
                var region = BuildRegionLabel(alert);
                var risks = alert.Riscos is { Count: > 0 }
                    ? string.Join(" ", alert.Riscos.Where(static r => !string.IsNullOrWhiteSpace(r)))
                    : null;
                var instructions = alert.Instrucoes is { Count: > 0 }
                    ? string.Join(" ", alert.Instrucoes.Where(static i => !string.IsNullOrWhiteSpace(i)))
                    : null;

                var severity = ParseSeverityText(alert.Severidade);
                markers.Add(new GeoMarkerDto(
                    $"inmet-{alert.Id}",
                    "weather_alert",
                    title,
                    severity,
                    centroid.Value.Lat,
                    centroid.Value.Lng,
                    Weight: ParseSeveridade(alert.Severidade),
                    Timestamp: ParseDate(alert.DataInicio, alert.HoraInicio),
                    Detail: alert.Descricao,
                    Source: "INMET · Brasil",
                    Severity: severity,
                    Region: region,
                    ValidUntil: ParseDate(alert.DataFim, alert.HoraFim),
                    EventType: alert.Descricao,
                    Instructions: instructions,
                    Risks: risks));
            }

            return markers;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogDebug(ex, "INMET avisos indisponível.");
            return [];
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "INMET avisos: resposta JSON inesperada.");
            return [];
        }
    }

    private static string? BuildRegionLabel(InmetAlert alert)
    {
        if (!string.IsNullOrWhiteSpace(alert.Estados))
        {
            return TruncateList(alert.Estados, 120);
        }

        if (!string.IsNullOrWhiteSpace(alert.Regioes))
        {
            return TruncateList(alert.Regioes, 120);
        }

        return null;
    }

    private static string TruncateList(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
    }

    private static string? ParseSeverityText(JsonElement? severidade)
    {
        if (severidade is null)
        {
            return null;
        }

        return severidade.Value.ValueKind switch
        {
            JsonValueKind.String => severidade.Value.GetString(),
            JsonValueKind.Number when severidade.Value.TryGetDouble(out var number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => severidade.Value.ToString()
        };
    }

    private static double ParseSeveridade(JsonElement? severidade)
    {
        if (severidade is null)
        {
            return 1;
        }

        var value = severidade.Value;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out number))
            {
                return number;
            }

            return text?.ToLowerInvariant() switch
            {
                "extrema" or "extreme" => 3,
                "alta" or "high" => 2.5,
                "moderada" or "moderate" => 2,
                "baixa" or "low" => 1,
                _ => 1
            };
        }

        return 1;
    }

    private static (double Lat, double Lng)? ParseCentroid(string? polygonJson)
    {
        if (string.IsNullOrWhiteSpace(polygonJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(polygonJson);
            return Aegis.Infrastructure.Geo.PolygonCentroidHelper.FromGeoJson(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? ParseDate(string? date, string? time)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return null;
        }

        var value = string.IsNullOrWhiteSpace(time) ? date : $"{date[..10]}T{time}:00Z";
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private sealed class InmetAlertResponse
    {
        [JsonPropertyName("hoje")]
        public List<InmetAlert>? Hoje { get; set; }
    }

    private sealed class InmetAlert
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("titulo")]
        public string? Titulo { get; set; }

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }

        [JsonPropertyName("poligono")]
        public string? Poligono { get; set; }

        [JsonPropertyName("data_inicio")]
        public string? DataInicio { get; set; }

        [JsonPropertyName("hora_inicio")]
        public string? HoraInicio { get; set; }

        [JsonPropertyName("data_fim")]
        public string? DataFim { get; set; }

        [JsonPropertyName("hora_fim")]
        public string? HoraFim { get; set; }

        [JsonPropertyName("estados")]
        public string? Estados { get; set; }

        [JsonPropertyName("regioes")]
        public string? Regioes { get; set; }

        [JsonPropertyName("riscos")]
        public List<string>? Riscos { get; set; }

        [JsonPropertyName("instrucoes")]
        public List<string>? Instrucoes { get; set; }

        [JsonPropertyName("severidade")]
        public JsonElement? Severidade { get; set; }
    }
}
