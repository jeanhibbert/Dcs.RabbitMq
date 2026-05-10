using System;

namespace Dcs.Messaging.Resiliency
{
    /// <summary>
    /// Read-only view of a session's connection state. Both
    /// <see cref="Tcp.TcpMessagingSession"/> and
    /// <see cref="Grpc.GrpcMessagingSession"/> implement this so applications
    /// can react to disconnect / reconnect events without depending on the
    /// transport.
    /// </summary>
    public interface IConnectionStateObserver
    {
        ConnectionState CurrentState { get; }

        IObservable<ConnectionState> StateChanged { get; }
    }
}
