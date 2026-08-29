using System.Threading.Channels;
using Aegis.Application.Dtos.Flights;

namespace Aegis.Web.Services;

public sealed class CircuitFlightFeed : IAsyncDisposable
{
    private readonly Channel<FlightSnapshot> _channel;
    private readonly CancellationTokenSource _cts = new();
    private Task? _pumpTask;

    public CircuitFlightFeed(Channel<FlightSnapshot> channel)
    {
        _channel = channel;
        _pumpTask = PumpAsync(_cts.Token);
    }

    public FlightSnapshot? Latest { get; private set; }

    public event Action<FlightSnapshot>? SnapshotReceived;

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var snapshot in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                Latest = snapshot;
                SnapshotReceived?.Invoke(snapshot);
            }
        }
        catch (OperationCanceledException)
        {
            // circuit disposed
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        return ValueTask.CompletedTask;
    }
}
