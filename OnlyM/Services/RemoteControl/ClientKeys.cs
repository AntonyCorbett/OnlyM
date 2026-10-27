using System;
using System.Net;
using System.Net.Sockets;

namespace OnlyM.Services.RemoteControl;

/// <summary>
/// Throttling keys for a client address. IPv4 (including IPv4-mapped IPv6) is keyed by the full
/// address. IPv6 is keyed by its /64 prefix (<see cref="Narrow"/>, one subnet: a client can pick any
/// interface ID in it) and by its /48 prefix (<see cref="Wide"/>, one site: a client can usually rotate
/// the 4th hextet as well), so rotating addresses inside one allocation cannot dodge the limits.
/// </summary>
internal sealed class ClientKeys
{
    public const string Unknown = "unknown";

    private ClientKeys(string narrow, string? wide)
    {
        Narrow = narrow;
        Wide = wide;
    }

    /// <summary>
    /// Gets the key for the full IPv4 address or the IPv6 /64 prefix.
    /// </summary>
    public string Narrow { get; }

    /// <summary>
    /// Gets the key for the IPv6 /48 prefix, or null for IPv4 / unknown clients.
    /// </summary>
    public string? Wide { get; }

    /// <summary>
    /// Gets the throttling keys for a client address.
    /// </summary>
    /// <param name="address">Client address, or null when unknown.</param>
    /// <param name="source">Tag prefixed to the keys (e.g. "cf:" for tunnel clients), so tunnel and LAN clients never share a bucket.</param>
    /// <returns>The keys for the client.</returns>
    public static ClientKeys For(IPAddress? address, string source = "")
    {
        if (address == null)
        {
            return new ClientKeys(source + Unknown, null);
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return new ClientKeys(source + address, null);
        }

        return new ClientKeys(source + Prefix(address, 64), source + Prefix(address, 48));
    }

    public override string ToString() => Wide == null ? Narrow : $"{Narrow} ({Wide})";

    private static string Prefix(IPAddress address, int bits)
    {
        var bytes = address.GetAddressBytes(); // 16 bytes, scope ID not included
        var whole = bits / 8;
        Array.Clear(bytes, whole, bytes.Length - whole);
        return new IPAddress(bytes) + "/" + bits;
    }
}
