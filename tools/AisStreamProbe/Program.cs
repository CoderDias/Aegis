using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var apiKey = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("AISSTREAM_API_KEY") ?? "";
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Usage: AisStreamProbe <apiKey>");
    return 1;
}

using var ws = new ClientWebSocket();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
await ws.ConnectAsync(new Uri("wss://stream.aisstream.io/v0/stream"), cts.Token);
Console.WriteLine("Connected.");

var sub = JsonSerializer.Serialize(new
{
    APIKey = apiKey,
    BoundingBoxes = new[] { new[] { new[] { -90.0, -180.0 }, new[] { 90.0, 180.0 } } },
    FilterMessageTypes = new[] { "PositionReport", "ShipStaticData" }
});
await ws.SendAsync(Encoding.UTF8.GetBytes(sub), WebSocketMessageType.Text, true, cts.Token);
Console.WriteLine("Subscription sent.");

var buffer = new byte[32768];
var messages = 0;
var positions = 0;
var deadline = DateTime.UtcNow.AddSeconds(18);

while (DateTime.UtcNow < deadline && messages < 5)
{
    var result = await ws.ReceiveAsync(buffer, cts.Token);
    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
    messages++;

    using var doc = JsonDocument.Parse(text);
    if (doc.RootElement.TryGetProperty("MessageType", out var mt))
    {
        var type = mt.GetString();
        if (string.Equals(type, "PositionReport", StringComparison.OrdinalIgnoreCase))
        {
            positions++;
        }

        Console.WriteLine($"[{messages}] {type}: {text[..Math.Min(text.Length, 220)]}");
    }
    else
    {
        Console.WriteLine($"[{messages}] {text[..Math.Min(text.Length, 220)]}");
    }
}

Console.WriteLine($"Done. frames={messages}, positionReports={positions}");
await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
return positions > 0 || messages > 0 ? 0 : 2;
