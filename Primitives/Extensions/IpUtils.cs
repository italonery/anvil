using System.Net;
using System.Net.Sockets;

namespace Anvil.Primitives.Extensions;

public static class IpUtils
{
    public static string ResolveLocalIpv4()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }

            return "127.0.0.1";
        }
        catch
        {
            return "0.0.0.0";
        }
    }

    public static bool IsValidIpAddress(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (IPAddress.TryParse(input, out var ip))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString() == input;
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return true;
            }
        }

        return false;
    }
}
