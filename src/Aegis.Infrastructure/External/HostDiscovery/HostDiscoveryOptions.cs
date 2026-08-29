namespace Aegis.Infrastructure.External.HostDiscovery;

public sealed class HostDiscoveryOptions
{
    public const string SectionName = "HostDiscovery";

    public bool Enabled { get; set; } = true;

    /// <summary>Máximo de IPs amostrados por tile regional (~2°).</summary>
    public int MaxSamplesPerRegion { get; set; } = 36;

    /// <summary>Timeout por tentativa TCP (ms).</summary>
    public int PortScanTimeoutMs { get; set; } = 1200;

    /// <summary>Probes TCP em paralelo por IP.</summary>
    public int MaxConcurrentPortProbes { get; set; } = 6;

    /// <summary>IPs investigados em paralelo por região.</summary>
    public int MaxConcurrentHosts { get; set; } = 8;

    public int[] CommonPorts { get; set; } =
    [
        80, 443, 8080, 8443, 554, 37777, 8000, 8888, 21, 22, 23, 161, 502, 9100
    ];

    public bool UseInternetDb { get; set; } = true;

    public bool UseTcpProbe { get; set; } = true;

    /// <summary>Intervalo mínimo entre chamadas ip-api.com (plano gratuito ~45/min).</summary>
    public int GeolocationDelayMs { get; set; } = 1400;
}
