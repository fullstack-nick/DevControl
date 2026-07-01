using System.Net;
using System.Net.Sockets;
using DevControl.Application.Outbound;

namespace DevControl.Infrastructure.Outbound;

public sealed class OutboundRequestGuard(IOutboundDnsResolver dnsResolver)
{
    private static readonly HashSet<string> BlockedHostnames = new(StringComparer.OrdinalIgnoreCase)
    {
        "metadata.google.internal",
        "metadata.goog"
    };

    public async Task<OutboundGuardResult> ValidateAsync(
        Uri uri,
        OutboundRequestPolicy policy,
        CancellationToken cancellationToken)
    {
        if (!uri.IsAbsoluteUri)
        {
            return OutboundGuardResult.Blocked("Outbound URL must be absolute.");
        }

        if (uri.UserInfo.Length > 0)
        {
            return OutboundGuardResult.Blocked("Outbound URL cannot include user info.");
        }

        if (policy.RequireHttps && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return OutboundGuardResult.Blocked("Webhook URLs must use HTTPS.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return OutboundGuardResult.Blocked("Outbound URL scheme must be HTTP or HTTPS.");
        }

        var port = uri.IsDefaultPort
            ? string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : uri.Port;
        if (!policy.AllowedPorts.Contains(port))
        {
            return OutboundGuardResult.Blocked($"Outbound URL port {port} is not allowed.");
        }

        var host = uri.IdnHost.TrimEnd('.');
        if (string.IsNullOrWhiteSpace(host))
        {
            return OutboundGuardResult.Blocked("Outbound URL host is required.");
        }

        if (IsBlockedHostname(host))
        {
            return OutboundGuardResult.Blocked("Outbound URL host is internal.");
        }

        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await dnsResolver.ResolveAsync(host, cancellationToken);
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            return OutboundGuardResult.Blocked("Outbound URL host could not be resolved.");
        }

        if (addresses.Count == 0)
        {
            return OutboundGuardResult.Blocked("Outbound URL host did not resolve to an address.");
        }

        foreach (var address in addresses)
        {
            if (!IsPublicAddress(address))
            {
                return OutboundGuardResult.Blocked($"Outbound URL resolved to blocked address {address}.");
            }
        }

        var selectedAddress = addresses
            .OrderBy(address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .First();

        return OutboundGuardResult.Allowed(selectedAddress, port);
    }

    private static bool IsBlockedHostname(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            BlockedHostnames.Contains(host);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicIPv4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicIPv6(address.GetAddressBytes()),
            _ => false
        };
    }

    private static bool IsPublicIPv4(byte[] bytes)
    {
        var first = bytes[0];
        var second = bytes[1];

        if (first == 0 ||
            first == 10 ||
            first == 127 ||
            first == 169 && second == 254 ||
            first == 172 && second is >= 16 and <= 31 ||
            first == 192 && second == 168 ||
            first == 100 && second is >= 64 and <= 127 ||
            first == 192 && second == 0 ||
            first == 198 && second is 18 or 19 ||
            first >= 224)
        {
            return false;
        }

        return true;
    }

    private static bool IsPublicIPv6(byte[] bytes)
    {
        if (bytes.All(value => value == 0))
        {
            return false;
        }

        if (bytes[..15].All(value => value == 0) && bytes[15] == 1)
        {
            return false;
        }

        var first = bytes[0];
        var second = bytes[1];
        if ((first & 0xFE) == 0xFC ||
            first == 0xFE && (second & 0xC0) == 0x80 ||
            first == 0xFF)
        {
            return false;
        }

        return true;
    }
}

public sealed record OutboundGuardResult(bool IsAllowed, IPAddress? Address, int Port, string? Error)
{
    public static OutboundGuardResult Allowed(IPAddress address, int port) => new(true, address, port, null);

    public static OutboundGuardResult Blocked(string error) => new(false, null, 0, error);
}
