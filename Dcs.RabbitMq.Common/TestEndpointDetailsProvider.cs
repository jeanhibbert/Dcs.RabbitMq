using Dcs.RabbitMq.Messaging;
using Dcs.RabbitMq.Messaging.Messaging;
using System.Collections.Generic;

namespace Dcs.RabbitMq.Common
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
            _endpoints.Add("TEST_ALERTS", new RabbitMqEndpointDetails("Test/Alerts", RabbitMqEndpointType.Default));
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
