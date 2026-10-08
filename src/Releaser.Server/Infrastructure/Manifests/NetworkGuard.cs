using System.Net;
using System.Net.Sockets;

namespace Releaser.Server.Infrastructure.Manifests;

/// <summary>Classifies addresses that must not be reachable from manifest fetching (SSRF protection, ADR 0003).</summary>
internal static class NetworkGuard
{
    private static readonly (IPAddress Network, int PrefixLength)[] BlockedV4 =
    [
        (IPAddress.Parse("0.0.0.0"), 8),
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("100.64.0.0"), 10),
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16),
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.168.0.0"), 16),
        (IPAddress.Parse("224.0.0.0"), 4),
        (IPAddress.Parse("240.0.0.0"), 4),
    ];

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !BlockedV4.Any(block => new IPNetwork(block.Network, block.PrefixLength).Contains(address));
        }
        return !(IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.IPv6None)
            || address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal
            || address.IsIPv6UniqueLocal
            || address.IsIPv6Multicast);
    }
}
