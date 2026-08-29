using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.OpenSky;

public sealed class OpenSkyTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<OpenSkyOptions> options,
    ILogger<OpenSkyTokenProvider> logger)
{
    private const string TokenUrl =
        "https://auth.opensky-network.org/auth/realms/opensky-network/protocol/openid-connect/token";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.CurrentValue.ClientId) &&
        !string.IsNullOrWhiteSpace(options.CurrentValue.ClientSecret);

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _accessToken;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _accessToken;
            }

            var cfg = options.CurrentValue;
            var client = httpClientFactory.CreateClient();
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = cfg.ClientId,
                ["client_secret"] = cfg.ClientSecret
            });

            using var response = await client.PostAsync(TokenUrl, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenSky OAuth failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!doc.RootElement.TryGetProperty("access_token", out var tokenProp))
            {
                return null;
            }

            _accessToken = tokenProp.GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expProp)
                ? expProp.GetInt32()
                : 1800;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(expiresIn - 60, 60));

            logger.LogInformation("OpenSky OAuth token refreshed (expires in {Seconds}s)", expiresIn);
            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed class OpenSkyAuthHandler(OpenSkyTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
