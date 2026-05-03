using Dcs.Messaging;
using System.Collections.Generic;

namespace Dcs.Messaging.Common
{
    public class TestEndpointDetailsProvider : IEndpointDetailsProvider
    {
        private readonly Dictionary<string, IEndpointDetails> _endpoints = new Dictionary<string, IEndpointDetails>();

        public Dictionary<string, IEndpointDetails> Endpoints
        {
            get { return _endpoints; }
        }

        public TestEndpointDetailsProvider()
        {
            _endpoints.Add("TEST_ALERTS", new MessageEndpointDetails("Test/Alerts", EndpointType.Default));
            _endpoints.Add("TEST_PRICING_REQUESTS", new MessageEndpointDetails("Test/Pricing/Requests", EndpointType.Default));
            _endpoints.Add("TEST_PRICING_RESPONSES", new MessageEndpointDetails("Test/Pricing/Responses", EndpointType.Default));
        }


        #region IEndpointDetailsProvider Members

        public IEndpointDetails GetEndpointDetails(string endpointKey)
        {
            return _endpoints[endpointKey];
        }

        public bool TryGetEndpointDetails(
            string endpointKey,
            out IEndpointDetails endpointDetails)
        {
            return _endpoints.TryGetValue(endpointKey, out endpointDetails);
        }

        IDictionary<string, IEndpointDetails> IEndpointDetailsProvider.GetEndpoints()
        {
            return new Dictionary<string, IEndpointDetails>(_endpoints);
        }

        #endregion
    }
}
