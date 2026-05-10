using System.Net;
using System.Net.Sockets;

namespace Dcs.Messaging.IntegrationTests.Infrastructure;

/// <summary>
/// Allocates a free TCP port from the OS. The port is then released so the
/// caller can bind it. Two test classes binding ports concurrently could in
/// theory race, but xUnit runs tests inside a class sequentially and our
/// classes use distinct port ranges to make collisions unlikely.
/// </summary>
internal static class PortAllocator
{
    public static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
