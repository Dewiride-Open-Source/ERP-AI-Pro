using System.Net;
using System.Net.Sockets;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

// An IPv6 site is normally given a whole /64 and a client can rotate through it freely, so one partition per /64 keeps a
// single client from creating unbounded partitions and escaping its limit.
internal static class ClientAddressPartition
{
    public const string Unknown = "unknown";

    private const int Ipv6PrefixBytes = 8;

    public static string For(IPAddress? address)
    {
        if (address is null)
        {
            return Unknown;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[Ipv6PrefixBytes..].Clear();

        return $"{new IPAddress(bytes)}/64";
    }
}
