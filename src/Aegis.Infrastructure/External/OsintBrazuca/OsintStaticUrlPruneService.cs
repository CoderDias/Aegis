using Aegis.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.OsintBrazuca;

public sealed class OsintStaticUrlPruneService(
    IOsintBrazucaCatalog catalog,
    OsintBlockedUrlStore blockedStore,
    IHttpClientFactory httpClientFactory,
    ILogger<OsintStaticUrlPruneService> logger) : BackgroundService
{
    private const int BatchSize = 25;
    private static readonly TimeSpan BatchDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);

        var sources = catalog.GetAllSources();
        if (sources.Count == 0)
        {
            return;
        }

        logger.LogInformation("Verificando {Count} URLs estáticas OSINT (404 → bloqueio)...", sources.Count);
        var blocked = 0;

        for (var i = 0; i < sources.Count && !stoppingToken.IsCancellationRequested; i += BatchSize)
        {
            var batch = sources.Skip(i).Take(BatchSize);
            foreach (var source in batch)
            {
                if (blockedStore.IsBlocked(source.Url))
                {
                    continue;
                }

                var statusCode = await ProbeStatusCodeAsync(source.Url, stoppingToken).ConfigureAwait(false);
                if (statusCode == 404)
                {
                    blockedStore.Block(source.Url, 404);
                    blocked++;
                }
            }

            await Task.Delay(BatchDelay, stoppingToken).ConfigureAwait(false);
        }

        if (blocked > 0)
        {
            logger.LogInformation("OSINT: {Count} URLs estáticas bloqueadas por 404.", blocked);
        }
    }

    private async Task<int?> ProbeStatusCodeAsync(string url, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("OsintHealth");

        foreach (var method in new[] { HttpMethod.Head, HttpMethod.Get })
        {
            try
            {
                using var request = new HttpRequestMessage(method, url);
                using var response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if ((int)response.StatusCode == 404 && method == HttpMethod.Head)
                {
                    continue;
                }

                return (int)response.StatusCode;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (HttpRequestException)
            {
                if (method == HttpMethod.Get)
                {
                    return null;
                }
            }
        }

        return null;
    }
}
