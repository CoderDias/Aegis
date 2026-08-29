using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Aegis.Infrastructure.External.HostDiscovery;

public readonly record struct CidrBlock(uint Network, int PrefixLength)
{
    public static bool TryParse(string line, out CidrBlock block)
    {
        block = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        var slash = trimmed.IndexOf('/');
        if (slash <= 0)
        {
            return false;
        }

        if (!IPAddress.TryParse(trimmed[..slash], out var address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        if (!int.TryParse(trimmed[(slash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix) ||
            prefix is < 0 or > 32)
        {
            return false;
        }

        var network = IpAddressHelper.ToUInt32(address);
        if (prefix > 0)
        {
            var mask = uint.MaxValue << (32 - prefix);
            network &= mask;
        }

        block = new CidrBlock(network, prefix);
        return true;
    }
}

internal static class CidrSampler
{
    public static string SampleIp(CidrBlock block, Random random)
    {
        var hostBits = 32 - block.PrefixLength;
        if (hostBits <= 0)
        {
            return IpAddressHelper.FromUInt32(block.Network);
        }

        uint offset;
        if (hostBits >= 31)
        {
            offset = 1;
        }
        else
        {
            var maxHost = (1u << hostBits) - 2;
            offset = (uint)random.NextInt64(1, maxHost + 1);
        }

        return IpAddressHelper.FromUInt32(block.Network + offset);
    }
}

internal static class IpAddressHelper
{
    public static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    public static string FromUInt32(uint value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value >> 24}.{(value >> 16) & 0xFF}.{(value >> 8) & 0xFF}.{value & 0xFF}");
}
