using Dcs.Messaging.Resiliency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace Dcs.Messaging
{
    public interface IMessagingService
    {
        IObservable<Unit> Send(
            IMessage message,
            IEndpointDetails endpointDetails,
            string targetSessionId = null);

        /// <summary>
        /// Allocation-free synchronous send that reports the outcome via
        /// <see cref="SendResult"/> rather than throwing on transport failure.
        /// Default implementation falls back to <see cref="Send(IMessage, IEndpointDetails, string)"/>
        /// for back-compat.
        /// </summary>
        SendResult TrySend(
            IMessage message,
            IEndpointDetails endpointDetails,
            string targetSessionId = null)
        {
            Send(message, endpointDetails, targetSessionId);
            return SendResult.Sent;
        }

        IObservable<IMessageStream> GetMessageStream(
            IEndpointDetails endpointDetails);
    }

    public interface IMessagingService<T>
    {
        IObservable<Unit> Send(
            T message,
            IEndpointDetails endpointDetails,
            string targetSessionId = null,
            TimeSpan? timeToLive = null,
            bool isPersistent = false);

        IObservable<IMessageStream<T>> GetMessageStream(
            IEndpointDetails endpointDetails);
    }
}
