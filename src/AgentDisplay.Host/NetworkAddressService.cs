using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AgentDisplay.Host;

public sealed class NetworkAddressService
{
    public IReadOnlyList<string> CandidateHostUrls(HttpRequest request)
    {
        var scheme = request.Scheme;
        var port = request.Host.Port ?? (scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 5277);
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up &&
                        x.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses)
            .Select(x => x.Address)
            .Where(x => x.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x) && !x.ToString().StartsWith("169.254.", StringComparison.Ordinal))
            .Distinct()
            .OrderByDescending(IsPrivate)
            .ThenBy(x => x.ToString(), StringComparer.Ordinal)
            .Select(x => $"{scheme}://{x}:{port}")
            .ToList();

        var requested = $"{scheme}://{request.Host}";
        var isLoopbackHost = request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        if (IPAddress.TryParse(request.Host.Host, out var requestedAddress))
        {
            isLoopbackHost = IPAddress.IsLoopback(requestedAddress);
        }
        if (!isLoopbackHost)
        {
            candidates.Insert(0, requested);
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               bytes[0] == 192 && bytes[1] == 168 ||
               bytes[0] == 172 && bytes[1] is >= 16 and <= 31;
    }

    public bool IsLocalDeviceTarget(Uri uri)
    {
        if (uri.IsLoopback) return true;
        if (uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return true;
        if (!IPAddress.TryParse(uri.Host, out var address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork) return IsPrivate(address) || address.GetAddressBytes() is [169, 254, _, _];
        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.GetAddressBytes()[0] is 0xfc or 0xfd;
    }
}
