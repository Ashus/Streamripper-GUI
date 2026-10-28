namespace StreamripperGui.Core.Models;

public static class RelayEndpoint
{
    public static string? ToUrl(string? address, int? port)
    {
        if (port is not (>= 1 and <= 65535)) return null;
        if (string.IsNullOrWhiteSpace(address)) return $"http://localhost:{port}/";
        if (!System.Net.IPAddress.TryParse(address, out var ip)) return null;
        if (ip.Equals(System.Net.IPAddress.Any)) return $"http://localhost:{port}/";
        if (ip.Equals(System.Net.IPAddress.IPv6Any)) ip = System.Net.IPAddress.IPv6Loopback;
        var host = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{ip}]" : ip.ToString();
        return $"http://{host}:{port}/";
    }
}
