# Dcs.Messaging

Dcs.Messaging is a protocol-agnostic .NET messaging framework that supports **TCP** and **gRPC** transports with a unified API. Application code is written against abstractions; switching transport is a one-line builder change.

## Table of Contents

- [Solution Structure](#solution-structure)
- [Namespace Overview](#namespace-overview)
- [Architecture Overview](#architecture-overview)
- [Getting Started](#getting-started)
- [Transports](#transports)
- [Running the Samples](#running-the-samples)

---

## Solution Structure

```mermaid
graph TD
    subgraph core [Core Libraries]
        MSG["Dcs.Messaging<br/>Transport abstractions, TCP impl,<br/>gRPC impl, serialization"]
        CMN["Dcs.Messaging.Common<br/>Builder hierarchy, DTOs,<br/>endpoint config"]
    end

    subgraph tcpApps [TCP Sample Apps]
        TSRV["SkeletonServerApp<br/>(TCP server)"]
        TCLI["SkeletonClientApp<br/>(TCP client)"]
    end

    subgraph grpcApps [gRPC Sample Apps]
        GSRV["SkeletonServerApp.Grpc<br/>(gRPC server)"]
        GCLI["SkeletonClientApp.Grpc<br/>(gRPC client)"]
    end

    subgraph tests [Tests]
        UT["Dcs.Messaging.UnitTests<br/>(xUnit)"]
    end

    CMN --> MSG
    TSRV --> CMN
    TCLI --> CMN
    GSRV --> CMN
    GSRV --> TSRV
    GCLI --> CMN
    UT --> CMN
    UT --> MSG
```

| Project | Description |
|---------|-------------|
| **Dcs.Messaging** | Core library: transport abstractions (`IMessagingService`, `IEndpoint`, `IEndpointProvider`), TCP and gRPC session implementations, protobuf serialization, shared `TransportEnvelope` wire format. |
| **Dcs.Messaging.Common** | Composition root: `IMessagingSessionBuilder` interface, `MessagingSessionBuilderBase` abstract class, `TcpMessagingSessionBuilder`, `GrpcMessagingSessionBuilder`, DTOs, and endpoint configuration. |
| **Dcs.Messaging.SkeletonServerApp** | Sample TCP server demonstrating alerts (pub/sub) and pricing (request/response). |
| **Dcs.Messaging.SkeletonClientApp** | Sample TCP client that subscribes to alerts and requests pricing. |
| **Dcs.Messaging.SkeletonServerApp.Grpc** | Same server functionality over gRPC. Reuses the service classes from the TCP server project. |
| **Dcs.Messaging.SkeletonClientApp.Grpc** | Same client functionality over gRPC. |
| **Dcs.Messaging.UnitTests** | xUnit tests covering transport types, serialization, and builder wiring. |

---

## Namespace Overview

| Namespace | Purpose |
|-----------|---------|
| `Dcs.Messaging` | Core public abstractions and shared message types, including `IMessagingService`, `IMessage`, `IEndpoint`, `IEndpointDetails`, `MessageFactory`, and `TransportPropertyKeys`. |
| `Dcs.Messaging.Tcp` | TCP transport sessions, endpoints, endpoint providers, and TCP session options. |
| `Dcs.Messaging.Grpc` | gRPC transport sessions, endpoint providers, service contracts, and gRPC session options. |
| `Dcs.Messaging.RequestResponse` | Request/response correlation interfaces and responder implementation. |
| `Dcs.Messaging.Serialization` | Binary serializer abstractions and protobuf-net serializer. |
| `Dcs.Messaging.ServiceModel` | Attribute-based service model helpers. |
| `Dcs.Messaging.Commanding` | Command stream abstractions. |
| `Dcs.Messaging.Common` | Sample composition helpers, builders, DTOs, and endpoint configuration used by the skeleton apps. |

---

## Architecture Overview

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed diagrams covering:

- Builder class hierarchy
- Internal layering and message flow
- Send and receive sequences
- Request/response pattern

---

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later

### Building

```bash
dotnet build Dcs.Messaging.sln
```

### Running Tests

```bash
dotnet test Dcs.Messaging.sln
```

### Quick Start (TCP)

**Server:**

```csharp
var endpointProvider = new TestEndpointDetailsProvider();
var serverId = Guid.NewGuid().ToString();
var options = TcpSessionOptions.CreateServer("127.0.0.1", 5050, serverId);
var builder = new TcpMessagingSessionBuilder("MyServer", endpointProvider, options);

using (builder.MessagingTransport)
{
    // Use builder.MessagingService to send/receive
    // Use builder.RequestResponder for request/response
    Console.ReadLine();
}
```

**Client:**

```csharp
var endpointProvider = new TestEndpointDetailsProvider();
var clientId = Guid.NewGuid().ToString();
var options = TcpSessionOptions.CreateClient("127.0.0.1", 5050, clientId);
var builder = new TcpMessagingSessionBuilder("MyClient", endpointProvider, options);

using (builder.MessagingTransport)
{
    // Use builder.MessagingService to send/receive
    Console.ReadLine();
}
```

### Quick Start (gRPC)

The only change is the builder and options type:

**Server:**

```csharp
var options = GrpcSessionOptions.CreateServer("http://127.0.0.1:5050", serverId);
var builder = new GrpcMessagingSessionBuilder("MyServer", endpointProvider, options);
```

**Client:**

```csharp
var options = GrpcSessionOptions.CreateClient("http://127.0.0.1:5050", clientId);
var builder = new GrpcMessagingSessionBuilder("MyClient", endpointProvider, options);
```

All downstream code (`MessagingService`, `RequestResponder`, `Serializer`, etc.) is identical regardless of transport.

---

## Transports

### TCP

- Uses raw TCP sockets with a **4-byte big-endian length prefix** followed by protobuf-serialized `TransportEnvelope` bytes.
- Server binds to `host:port` and accepts multiple client connections.
- Client connects to a single server endpoint.
- Configure via `TcpSessionOptions.CreateServer(host, port, sessionId)` / `TcpSessionOptions.CreateClient(host, port, sessionId)`.

### gRPC

- Uses **protobuf-net.Grpc** (code-first) with a single **bidirectional streaming** RPC (`Exchange`).
- Server runs an embedded **Kestrel** HTTP/2 host via `WebApplication`.
- Client connects via `GrpcChannel.ForAddress`.
- For local development without TLS, HTTP/2 cleartext (h2c) is enabled by default (`EnableHttp2Unencrypted = true` on options).
- Configure via `GrpcSessionOptions.CreateServer(baseUrl, sessionId)` / `GrpcSessionOptions.CreateClient(baseUrl, sessionId)`.

---

## Running the Samples

### TCP pair

In two separate terminals:

```bash
# Terminal 1 - Server
dotnet run --project Dcs.Messaging.SkeletonServerApp

# Terminal 2 - Client
dotnet run --project Dcs.Messaging.SkeletonClientApp
```

### gRPC pair

```bash
# Terminal 1 - Server
dotnet run --project Dcs.Messaging.SkeletonServerApp.Grpc

# Terminal 2 - Client
dotnet run --project Dcs.Messaging.SkeletonClientApp.Grpc
```

> **Note:** Both TCP and gRPC samples default to port 5050. Do not run a TCP server and gRPC server simultaneously on the same port.
