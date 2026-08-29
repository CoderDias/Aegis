using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.HostDiscovery;

public sealed class TcpPortProbe(IOptions<HostDiscoveryOptions> options)
{
    public async Task<IReadOnlyList<int>> ScanAsync(string ip, CancellationToken cancellationToken = default) =>
        await ScanPortsAsync(ip, options.Value.CommonPorts, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<int>> ScanPortsAsync(
        string ip,
        IEnumerable<int> ports,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.UseTcpProbe)
        {
            return [];
        }

        var timeoutMs = options.Value.PortScanTimeoutMs;
        var maxParallel = Math.Max(1, options.Value.MaxConcurrentPortProbes);
        var open = new List<int>();

        await Parallel.ForEachAsync(
            ports,
            new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = cancellationToken },
            async (port, token) =>
            {
                if (await IsPortOpenAsync(ip, port, timeoutMs, token).ConfigureAwait(false))
                {
                    lock (open)
                    {
                        open.Add(port);
                    }
                }
            }).ConfigureAwait(false);

        return open.OrderBy(p => p).ToList();
    }

    private static async Task<bool> IsPortOpenAsync(string ip, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);
            await client.ConnectAsync(ip, port, timeoutCts.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
