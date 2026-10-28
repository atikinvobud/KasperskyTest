
using System.Net;
using System.Net.Sockets;

namespace Backend.Scanning;

public class RepositoryUrlValidator
{
    private static readonly HashSet<string> AllowedHosts =
    [
        "github.com",
        "gitlab.com",
        "bitbucket.org"
    ];

    public async Task<bool> IsValidAsync(
        string? repositoryUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl)) return false;

        if (!Uri.TryCreate(repositoryUrl,UriKind.Absolute,out var uri)) return false;

        if (uri.Scheme != Uri.UriSchemeHttps|| !string.IsNullOrEmpty(uri.UserInfo) || uri.Port != 443 || !AllowedHosts.Contains(uri.IdnHost))
        {
            return false;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost,cancellationToken);

            if (addresses.Length == 0) return false;

            return addresses.All(IsPublicAddress);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address)) return false;


        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            {
                return false;
            }

            var bytes = address.GetAddressBytes();

            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return false;
            }

            if (address.Equals(IPAddress.IPv6Any))
            {
                return false;
            }

            if (bytes[0] == 0x20
                && bytes[1] == 0x01
                && bytes[2] == 0x0D
                && bytes[3] == 0xB8)
            {
                return false;
            }

            return (bytes[0] & 0xE0) == 0x20;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var ipv4 = address.GetAddressBytes();
        var a = ipv4[0];
        var b = ipv4[1];
        var c = ipv4[2];

        if (a == 0 || a == 10 || a == 127 || a >= 224)
        {
            return false;
        }

        if (a == 100 && b >= 64 && b <= 127) return false;
        if (a == 169 && b == 254) return false;
        if (a == 172 && b >= 16 && b <= 31) return false;
        if (a == 192 && b == 168) return false;

        if (a == 192 && b == 0 && c == 0) return false;
        if (a == 192 && b == 0 && c == 2) return false;
        if (a == 192 && b == 88 && c == 99) return false;
        if (a == 198 && (b == 18 || b == 19)) return false;
        if (a == 198 && b == 51 && c == 100) return false;
        if (a == 203 && b == 0 && c == 113) return false;

        return true;
    }
}
