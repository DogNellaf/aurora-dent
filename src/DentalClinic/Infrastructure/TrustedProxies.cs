using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace DentalClinic.Infrastructure
{
    /// <summary>
    /// X-Forwarded-* headers are only believed when they come from a configured proxy network. Trusting every
    /// sender would let any client claim any address or scheme.
    /// </summary>
    public static class TrustedProxies
    {
        public static void Apply(ForwardedHeadersOptions options, IEnumerable<string> cidrs)
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var cidr in cidrs.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                var network = System.Net.IPNetwork.Parse(cidr.Trim());
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(network.BaseAddress, network.PrefixLength));
            }
        }

        public static bool IsTrusted(ForwardedHeadersOptions options, IPAddress address) =>
            options.KnownNetworks.Any(n => n.Contains(address)) || options.KnownProxies.Contains(address);
    }
}
