namespace StreamripperGui.Core.Tests;

public sealed class RelayEndpointTests
{
    [Xunit.Theory]
    [Xunit.InlineData(null, 8001, "http://localhost:8001/")]
    [Xunit.InlineData("0.0.0.0", 8002, "http://localhost:8002/")]
    [Xunit.InlineData("127.0.0.1", 8003, "http://127.0.0.1:8003/")]
    [Xunit.InlineData("192.168.1.20", 9000, "http://192.168.1.20:9000/")]
    [Xunit.InlineData("::", 8004, "http://[::1]:8004/")]
    [Xunit.InlineData("2001:db8::5", 8005, "http://[2001:db8::5]:8005/")]
    [Xunit.InlineData("127.0.0.1", null, null)]
    [Xunit.InlineData("127.0.0.1", 0, null)]
    [Xunit.InlineData("127.0.0.1", 65536, null)]
    [Xunit.InlineData("not-an-ip", 8000, null)]
    public void LinkUsesActualPortAndAConnectableAddress(string? address, int? port, string? expected) =>
        Xunit.Assert.Equal(expected, Models.RelayEndpoint.ToUrl(address, port));
}
