global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Reactive.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Dcs.Messaging.IntegrationTests.Infrastructure;

// Integration tests bind to real loopback ports. Disable cross-class parallel
// execution so two tests don't race on PortAllocator and end up bound to the
// same TCP/HTTP2 port.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
