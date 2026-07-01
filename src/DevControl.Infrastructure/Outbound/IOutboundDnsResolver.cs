using System.Net;

namespace DevControl.Infrastructure.Outbound;

public interface IOutboundDnsResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class SystemOutboundDnsResolver : IOutboundDnsResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var parsed))
        {
            return [parsed];
        }

        return await Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}
