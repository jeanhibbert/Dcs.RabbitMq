using Dcs.Messaging.Common;
using Dcs.Messaging.Common.Dto;
using Dcs.Messaging.RequestResponse;
using System;

namespace Dcs.Messaging.SkeletonServerApp.Services
{
    public sealed class PricingService : IDisposable
    {
        private readonly IRequestResponder _requestResponder;
        private readonly IMessagingSessionBuilder _sessionBuilder;
        private IDisposable _subscription;

        public PricingService(IMessagingSessionBuilder sessionBuilder)
        {
            _requestResponder = sessionBuilder.RequestResponder;
            _sessionBuilder = sessionBuilder;
        }

        public void Start()
        {
            var requestEndpoint = _sessionBuilder.EndpointDetailsProvider.GetEndpointDetails(EndpointKeys.TestPricingRequests);
            var responseEndpoint = _sessionBuilder.EndpointDetailsProvider.GetEndpointDetails(EndpointKeys.TestPricingResponses);
            _requestResponder.InitializeEndpoint(requestEndpoint, responseEndpoint);
            _subscription = _requestResponder
                .GetRespondableRequestStream<PricingRequestDto, ForwardPricingDto>(requestEndpoint, responseEndpoint)
                .Subscribe(request =>
                {
                    var response = new ForwardPricingDto
                    {
                        CurrencyPair = request.Request.CurrencyPair,
                        BidRate = Math.Round(request.Request.SpotRate - 0.00035, 5),
                        AskRate = Math.Round(request.Request.SpotRate + 0.00035, 5),
                        IsContra = false
                    };

                    Console.WriteLine(
                        "Handled pricing request from {0} for {1}/{2} {3}",
                        request.SenderSessionId,
                        response.CurrencyPair.BaseCcy,
                        response.CurrencyPair.QuoteCcy,
                        request.Request.Tenor);

                    request.Respond(response);
                });
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}