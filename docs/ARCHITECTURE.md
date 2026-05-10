# Architecture

This document describes the internal architecture of Dcs.Messaging, with diagrams illustrating how the components fit together.

## Builder Class Hierarchy

Applications create a session by instantiating a transport-specific builder. Both builders produce an `IMessagingSessionBuilder` with an identical API surface, so all downstream code is transport-agnostic.

```mermaid
classDiagram
    class IMessagingSessionBuilder {
        <<interface>>
        +IEndpointDetailsFactory EndpointDetailsFactory
        +IEndpointDetailsProvider EndpointDetailsProvider
        +IMessageFactory MessageFactory
        +IMessagingService MessagingService
        +IDisposable MessagingTransport
        +IRequestResponder RequestResponder
        +IBinarySerializer Serializer
    }

    class MessagingSessionBuilderBase {
        <<abstract>>
        #MessagingSessionBuilderBase(sessionName, provider, sessionId, transport, endpointProvider)
    }

    class TcpMessagingSessionBuilder {
        +TcpMessagingSessionBuilder(sessionName, provider, TcpSessionOptions)
    }

    class GrpcMessagingSessionBuilder {
        +GrpcMessagingSessionBuilder(sessionName, provider, GrpcSessionOptions)
    }

    IMessagingSessionBuilder <|.. MessagingSessionBuilderBase
    MessagingSessionBuilderBase <|-- TcpMessagingSessionBuilder
    MessagingSessionBuilderBase <|-- GrpcMessagingSessionBuilder
```

The abstract base class (`MessagingSessionBuilderBase`) wires the common services:

- `MessageEndpointDetailsFactory` -- creates endpoint detail objects
- `MessageFactory` -- creates `IMessage` instances stamped with the session ID
- `ProtobufNetBinarySerializer` -- protobuf-net based binary serializer
- `MessagingService` -- routes send/receive through the endpoint provider
- `RequestResponder` -- request/response correlation layer

Each concrete builder only needs to create its transport session and endpoint provider, then pass them to the base constructor.

---

## Internal Layering

The framework is structured in layers. Application code interacts with the public API; the transport layer is pluggable underneath.

```mermaid
flowchart TB
    subgraph appLayer [Application Layer]
        APP["Your Code<br/>(services, handlers)"]
    end

    subgraph apiLayer [Public API]
        ISB["IMessagingSessionBuilder"]
        IMS["IMessagingService<br/>.Send() / .GetMessageStream()"]
        IRR["IRequestResponder<br/>.GetRespondableRequestStream()"]
        SER["IBinarySerializer<br/>.Serialize() / .Deserialize()"]
        MF["IMessageFactory<br/>.Create()"]
    end

    subgraph plumbing [Plumbing]
        EP["IEndpointProvider"]
        END["IEndpoint<br/>.Send() / .MessageStream"]
    end

    subgraph transport [Transport Layer - swappable]
        direction LR
        subgraph tcpPath [TCP]
            TCPS["TcpMessagingSession<br/>length-prefixed protobuf<br/>over raw TCP sockets"]
        end
        subgraph grpcPath [gRPC]
            GRPCS["GrpcMessagingSession<br/>bidirectional streaming<br/>over HTTP/2"]
        end
    end

    subgraph wire [Shared Wire Format]
        ENV["TransportEnvelope<br/>(protobuf-net)"]
    end

    APP --> ISB
    ISB --> IMS
    ISB --> IRR
    ISB --> SER
    ISB --> MF
    IMS --> EP
    EP --> END
    END --> TCPS
    END --> GRPCS
    TCPS --> ENV
    GRPCS --> ENV
```

### Key interfaces

| Interface | Responsibility |
|-----------|---------------|
| `IMessagingSessionBuilder` | Entry point. Provides all services needed by application code. |
| `IMessagingService` | Send messages to and receive message streams from logical endpoints. |
| `IRequestResponder` | Higher-level request/response pattern with automatic correlation. |
| `IBinarySerializer` | Serialize/deserialize application payloads (protobuf-net). |
| `IMessageFactory` | Create `IMessage` instances with session identity and properties. |
| `IEndpointProvider` | Map `IEndpointDetails` to `IEndpoint` instances (transport-specific). |
| `IEndpoint` | Low-level send/receive on a single logical endpoint within a session. |

---

## Message Send Flow

When application code calls `MessagingService.Send()`, the message travels through these layers:

```mermaid
sequenceDiagram
    participant App as Application Code
    participant Svc as IMessagingService
    participant EProv as IEndpointProvider
    participant EP as IEndpoint
    participant Sess as Session (TCP or gRPC)
    participant Factory as TransportEnvelopeFactory
    participant Wire as Network

    App->>Svc: Send(message, endpointDetails)
    Svc->>EProv: GetEndpoint(endpointDetails)
    EProv-->>Svc: IEndpoint
    Svc->>EP: Send(message, targetSessionId)
    EP->>Sess: Send(endpointDetails, message, targetSessionId)
    Sess->>Factory: FromMessage(address, message, targetSessionId)
    Factory-->>Sess: TransportEnvelope
    alt TCP transport
        Sess->>Wire: 4-byte length prefix + protobuf bytes
    else gRPC transport
        Sess->>Wire: IAsyncEnumerable stream item (HTTP/2)
    end
```

### Targeting

- If `targetSessionId` is set, the message is delivered only to the connection whose remote session matches that ID.
- If `targetSessionId` is null/empty and the sender is a **server**, the message is broadcast to all connected clients.
- If the sender is a **client**, messages go to the single server connection.

---

## Message Receive Flow

Incoming messages are deserialized by the session, filtered by endpoint address and target session ID, and pushed into an Rx `ISubject`. Application code subscribes via `GetMessageStream()`.

```mermaid
sequenceDiagram
    participant Wire as Network
    participant Sess as Session (TCP or gRPC)
    participant Factory as TransportEnvelopeFactory
    participant Subject as ISubject (Rx)
    participant EP as IEndpoint
    participant Svc as IMessagingService
    participant App as Application Code

    Wire->>Sess: incoming bytes / stream item
    alt TCP transport
        Sess->>Sess: ReadEnvelopeAsync (length-prefixed protobuf)
    else gRPC transport
        Sess->>Sess: await foreach on IAsyncEnumerable
    end
    Sess->>Factory: ToMessage(envelope)
    Factory-->>Sess: IMessage
    Sess->>Subject: OnNext(IncomingTransportMessage)
    Note over Subject: filtered by endpoint address<br/>and target session ID
    App->>Svc: GetMessageStream(endpointDetails)
    Svc->>EP: .MessageStream
    EP->>Sess: GetMessageStream(endpointDetails)
    Sess-->>App: IObservable of IMessage (filtered stream)
```

---

## Request/Response Pattern

The `IRequestResponder` provides a higher-level abstraction over send/receive for correlated request/response flows.

```mermaid
sequenceDiagram
    participant Client as Client App
    participant CMF as MessageFactory
    participant CSvc as IMessagingService (client)
    participant Network as Network
    participant SSvc as IMessagingService (server)
    participant SRR as IRequestResponder (server)
    participant Handler as Server Handler

    Client->>CMF: Create(payload, ttl, persistent)
    Client->>Client: Set CorrelationId property
    Client->>CSvc: Send(message, requestEndpoint)
    CSvc->>Network: TransportEnvelope
    Network->>SSvc: incoming envelope
    SSvc->>SRR: GetRespondableRequestStream
    SRR->>Handler: IRespondableRequest
    Handler->>Handler: process request
    Handler->>SRR: request.Respond(response)
    SRR->>SSvc: Send(responseMessage, responseEndpoint, senderSessionId)
    SSvc->>Network: TransportEnvelope (targeted to client)
    Network->>CSvc: incoming envelope
    CSvc->>Client: filtered by CorrelationId
```

### How it works

1. The **client** creates a message, stamps it with a `CorrelationId` property, and sends it to the request endpoint.
2. The **server** subscribes to `IRequestResponder.GetRespondableRequestStream<TReq, TRes>(requestEndpoint, responseEndpoint)`. Each incoming request is exposed as an `IRespondableRequest<TReq, TRes>`.
3. The server handler calls `request.Respond(response)`, which serializes the response, stamps it with the same `CorrelationId`, and sends it back via the response endpoint targeted to the original sender's session ID.
4. The **client** filters incoming messages on the response endpoint by `CorrelationId` to match the reply.

---

## Wire Format: TransportEnvelope

Both TCP and gRPC use the same protobuf-serialized envelope (`TransportEnvelope`):

| ProtoMember | Field | Type | Description |
|-------------|-------|------|-------------|
| 1 | `EndpointAddress` | `string` | Logical endpoint the message belongs to |
| 2 | `SenderSessionId` | `string` | Session ID of the sender |
| 3 | `TargetSessionId` | `string` | If set, message is delivered only to this session |
| 4 | `Tag` | `string` | Optional message tag |
| 5 | `Properties` | `List<TransportPropertyValue>` | Key-value metadata (correlation ID, TTL, etc.) |
| 6 | `Payload` | `byte[]` | Serialized application payload |

Each `TransportPropertyValue` carries a `Key`, `Kind` (bool/double/int/string), and the corresponding typed value field. This allows message properties to round-trip across the wire without losing type information.

### TCP framing

```
[4 bytes: big-endian int32 payload length][N bytes: protobuf-serialized TransportEnvelope]
```

### gRPC framing

The `TransportEnvelope` is sent as individual items in a bidirectional `IAsyncEnumerable<TransportEnvelope>` stream. HTTP/2 handles framing.

---

## Endpoint Configuration

Endpoints are logical channels identified by string keys. An `IEndpointDetailsProvider` maps keys to `IEndpointDetails` objects (address + type).

The sample apps use `TestEndpointDetailsProvider` with three pre-configured endpoints:

| Key | Address | Used for |
|-----|---------|----------|
| `TEST_ALERTS` | `Test/Alerts` | Server broadcasts alert messages (pub/sub) |
| `TEST_PRICING_REQUESTS` | `Test/Pricing/Requests` | Client sends pricing requests |
| `TEST_PRICING_RESPONSES` | `Test/Pricing/Responses` | Server sends pricing responses back |

To add your own endpoints, implement `IEndpointDetailsProvider` or extend `TestEndpointDetailsProvider`.

---

## Adding a New Transport

The framework is designed to be extensible. To add a new transport (e.g. WebSocket, named pipes):

1. Create a new session class (like `TcpMessagingSession` or `GrpcMessagingSession`) that implements `IDisposable` and exposes `GetMessageStream()` and `Send()` methods.
2. Create a matching `IEndpointProvider` and `IEndpoint` implementation.
3. Create a new builder class extending `MessagingSessionBuilderBase`, passing your session and endpoint provider to the base constructor.

No existing code needs to change -- the Open/Closed principle is preserved by the builder hierarchy.

For resiliency, accept `IRetryPolicy` and `IClock` parameters in your session
constructor and use the same outbound-queue + supervisor pattern described
below.

---

## Resiliency

Both transports share the same resiliency model: a persistent outbound queue,
an automatic reconnect supervisor (client mode only), and structured
`SendResult` outcomes instead of exceptions.

### Component diagram

```mermaid
classDiagram
    class IConnectionStateObserver {
        <<interface>>
        +ConnectionState CurrentState
        +IObservable~ConnectionState~ StateChanged
    }

    class IRetryPolicy {
        <<interface>>
        +ExecuteAsync(operation, ct) Task~bool~
    }

    class IClock {
        <<interface>>
        +UtcNow DateTimeOffset
        +Delay(timespan, ct) Task
    }

    class ResiliencyOptions {
        +InitialBackoff TimeSpan
        +MaxBackoff TimeSpan
        +BackoffMultiplier double
        +UseJitter bool
        +OutboundQueueCapacity int
        +DropOldestWhenQueueFull bool
    }

    class SendResult {
        <<readonly struct>>
        +SendStatus Status
        +bool IsSuccess
    }

    class PollyRetryPolicy
    class SystemClock
    class NoRetryPolicy

    class TcpMessagingSession
    class GrpcMessagingSession

    IRetryPolicy <|.. PollyRetryPolicy
    IRetryPolicy <|.. NoRetryPolicy
    IClock <|.. SystemClock
    IConnectionStateObserver <|.. TcpMessagingSession
    IConnectionStateObserver <|.. GrpcMessagingSession
    TcpMessagingSession --> IRetryPolicy
    TcpMessagingSession --> IClock
    TcpMessagingSession --> ResiliencyOptions
    GrpcMessagingSession --> IRetryPolicy
    GrpcMessagingSession --> IClock
    GrpcMessagingSession --> ResiliencyOptions
```

### Send pipeline (no-throw)

```mermaid
sequenceDiagram
    participant App as Application Code
    participant Svc as IMessagingService
    participant EP as IEndpoint
    participant Sess as Session
    participant Queue as Outbound Channel~T~
    participant Writer as Writer Task
    participant Wire as Network

    App->>Svc: TrySend(message, endpoint)
    Svc->>EP: TrySend(message, sessionId)
    EP->>Sess: TrySend(...)
    Sess->>Sess: serialize TransportEnvelope
    Sess->>Queue: TryWrite(frame)
    alt write succeeds
        Queue-->>Sess: true
        Sess-->>App: SendResult.Buffered
    else queue full
        Queue-->>Sess: false
        Sess-->>App: SendResult.QueueFull
    end
    Note over Writer: drains queue independently
    Writer->>Queue: WaitToReadAsync / TryRead
    Queue-->>Writer: frame
    Writer->>Wire: write bytes
    alt write fails
        Writer->>Sess: requeue inflight frame
        Writer->>Sess: signal connection broken
        Note over Sess: supervisor reconnects<br/>and resumes draining
    end
```

### Client reconnect supervisor

```mermaid
stateDiagram-v2
    [*] --> Initializing
    Initializing --> Connecting: supervisor starts
    Connecting --> Connected: ConnectAsync succeeds
    Connecting --> Connecting: ConnectAsync fails<br/>(Polly backoff)
    Connected --> Disconnected: read or write fails
    Disconnected --> Connecting: supervisor wakes up<br/>after InitialBackoff
    Connected --> Closed: Dispose
    Connecting --> Closed: Dispose
    Disconnected --> Closed: Dispose
    Initializing --> Closed: Dispose
```

The supervisor loop is:

1. Set state `Connecting`.
2. Call `IRetryPolicy.ExecuteAsync` &mdash; the policy retries the connect
   attempt with exponential backoff until it succeeds or the session is
   cancelled.
3. Once connected, set state `Connected` and create a write loop that drains
   the **persistent** outbound queue.
4. If the write loop reports a transport failure, the in-flight frame is
   stored on the session and prepended to the next connection's writer. The
   supervisor then waits one `InitialBackoff` interval and starts again.
5. On `Dispose`, all loops are cancelled and the supervisor exits within the
   5-second drain timeout.

### Send-result mapping

| `SendStatus`     | When                                                                                  |
|------------------|---------------------------------------------------------------------------------------|
| `Sent`           | (Reserved for future synchronous-write fast paths.)                                   |
| `Buffered`       | The frame was successfully placed in the outbound queue.                              |
| `QueueFull`      | The outbound queue was full and `DropOldestWhenQueueFull` is `false`.                 |
| `NoSuchTarget`   | Server-side send: no connection matched the requested target session ID, or no clients are connected for a broadcast. |
| `ShuttingDown`   | The session has been disposed (or is in the process of disposing).                    |

### Why no `IObservable<Unit>` for resilient sends

The legacy `IMessagingService.Send(...)` returns an `IObservable<Unit>` whose
factory is scheduled on `Scheduler.Default`. That keeps the historical API
compatible but means a tight loop of `Send` calls can be reordered by the
thread pool. For ordering-sensitive code, use `IMessagingService.TrySend(...)`
which writes to the outbound queue synchronously on the calling thread.
