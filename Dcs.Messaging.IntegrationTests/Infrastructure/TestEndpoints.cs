using Dcs.Messaging;
using Dcs.Messaging.Common;

namespace Dcs.Messaging.IntegrationTests.Infrastructure;

internal static class TestEndpoints
{
    public static IEndpointDetails Alerts(IEndpointDetailsProvider provider)
        => provider.GetEndpointDetails(EndpointKeys.TestAlerts);

    public static IEndpointDetails PricingRequests(IEndpointDetailsProvider provider)
        => provider.GetEndpointDetails(EndpointKeys.TestPricingRequests);

    public static IEndpointDetails PricingResponses(IEndpointDetailsProvider provider)
        => provider.GetEndpointDetails(EndpointKeys.TestPricingResponses);
}
