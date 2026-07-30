using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.Estate;

public sealed class EstateGisNetworkSecurityOptions
{
    public const string SectionName = "EstateGisNetworkSecurity";

    public List<string> AllowedHosts { get; set; } = [];
}

public sealed class EstateGisNetworkPolicy
{
    private readonly EstateGisNetworkSecurityOptions _options;

    public EstateGisNetworkPolicy(IOptions<EstateGisNetworkSecurityOptions> options)
    {
        _options = options.Value;
    }

    public async Task ValidateHttpTargetAsync(
        string? value,
        string label,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException(
                $"{label} must be an absolute HTTP or HTTPS URL without embedded credentials.");
        }

        await ResolvePermittedAddressesAsync(uri.DnsSafeHost, label, cancellationToken);
    }

    public async Task ValidateHostAsync(
        string? host,
        string label,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        await ResolvePermittedAddressesAsync(host, label, cancellationToken);
    }

    public ValueTask<Stream> ConnectHttpAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
        => ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, cancellationToken);

    public async ValueTask<Stream> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        if (port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("GIS connection port must be between 1 and 65535.");
        }

        var addresses = await ResolvePermittedAddressesAsync(host, "GIS endpoint", cancellationToken);
        Exception? lastError = null;

        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                lastError = ex;
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("The approved GIS endpoint could not be reached.", lastError);
    }

    public async Task<IReadOnlyList<IPAddress>> ResolvePermittedAddressesAsync(
        string host,
        string label,
        CancellationToken cancellationToken)
    {
        var normalizedHost = NormalizeHost(host);
        if (string.IsNullOrWhiteSpace(normalizedHost)
            || Uri.CheckHostName(normalizedHost) == UriHostNameType.Unknown)
        {
            throw new InvalidOperationException($"{label} contains an invalid host name.");
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(normalizedHost, out var literalAddress))
        {
            addresses = [literalAddress];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(normalizedHost, cancellationToken);
            }
            catch (Exception ex) when (ex is SocketException or ArgumentException)
            {
                throw new InvalidOperationException($"{label} host could not be resolved.", ex);
            }
        }

        if (addresses.Length == 0)
        {
            throw new InvalidOperationException($"{label} host did not resolve to an address.");
        }

        if (!IsHostExplicitlyAllowed(normalizedHost)
            && addresses.Any(IsRestrictedAddress))
        {
            throw new InvalidOperationException(
                $"{label} resolves to a private or reserved network address that is not approved by the server administrator.");
        }

        return addresses;
    }

    public bool IsHostExplicitlyAllowed(string host)
    {
        var normalizedHost = NormalizeHost(host);
        return _options.AllowedHosts
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Any(item => HostMatches(normalizedHost, NormalizeHost(item)));
    }

    public static bool IsRestrictedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return IsRestrictedAddress(address.MapToIPv4());
        }

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.None)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0
                || bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 192 && bytes[1] == 0 && bytes[2] is 0 or 2)
                || (bytes[0] == 198 && bytes[1] is 18 or 19)
                || (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)
                || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)
                || bytes[0] >= 224;
        }

        return address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.IsIPv6SiteLocal
            || (bytes[0] & 0xFE) == 0xFC;
    }

    private static bool HostMatches(string host, string pattern)
    {
        if (string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = pattern[1..];
        return host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            && host.Length > suffix.Length;
    }

    private static string NormalizeHost(string value)
    {
        var host = value.Trim().TrimEnd('.');
        if (host.Length > 1 && host[0] == '[' && host[^1] == ']')
        {
            host = host[1..^1];
        }

        return host.ToLowerInvariant();
    }
}
