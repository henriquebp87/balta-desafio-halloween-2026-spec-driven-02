using System.Net;
using System.Net.Sockets;

namespace UrlShortener.Api.Application;

public sealed class TargetUrlValidator
{
    private static readonly IPNetwork[] NonPublicIpv4Networks =
    [
        IPNetwork.Parse("0.0.0.0/8"), IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"), IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.0.0.0/24"), IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.168.0.0/16"), IPNetwork.Parse("198.18.0.0/15"),
        IPNetwork.Parse("198.51.100.0/24"), IPNetwork.Parse("203.0.113.0/24"),
        IPNetwork.Parse("224.0.0.0/3")
    ];

    private readonly Func<string, CancellationToken, Task<IPAddress[]>> resolveAddresses;

    public TargetUrlValidator()
        : this((host, cancellationToken) => Dns.GetHostAddressesAsync(host, cancellationToken))
    {
    }

    public TargetUrlValidator(Func<string, CancellationToken, Task<IPAddress[]>> resolveAddresses)
    {
        ArgumentNullException.ThrowIfNull(resolveAddresses);
        this.resolveAddresses = resolveAddresses;
    }

    // A null result means valid; otherwise the result is a validation error.
    public async Task<string?> ValidateAsync(string? targetUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
            return "A URL de destino é obrigatória.";

        if (targetUrl.Length > 2048)
            return "A URL de destino deve ter no máximo 2048 caracteres.";

        if (targetUrl != targetUrl.Trim() || targetUrl.Any(char.IsControl) ||
            !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
            return "A URL de destino deve ser uma URL absoluta válida com http ou https.";

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.IdnHost, out var address))
        {
            addresses = [address];
        }
        else
        {
            try
            {
                addresses = await resolveAddresses(uri.IdnHost, cancellationToken);
            }
            catch (SocketException)
            {
                return "A URL de destino deve ser publicamente acessível.";
            }
        }

        return addresses.Length > 0 && addresses.All(IsPublicAddress)
            ? null
            : "A URL de destino deve ser publicamente acessível.";
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return !NonPublicIpv4Networks.Any(network => network.Contains(address));

        // Accept global IPv6 unicast, excluding special-purpose and documentation ranges.
        return address.AddressFamily == AddressFamily.InterNetworkV6 &&
            IPNetwork.Parse("2000::/3").Contains(address) &&
            !IPNetwork.Parse("2001::/23").Contains(address) &&
            !IPNetwork.Parse("2001:db8::/32").Contains(address) &&
            !IPNetwork.Parse("2002::/16").Contains(address) &&
            !IPNetwork.Parse("3fff::/20").Contains(address);
    }
}
