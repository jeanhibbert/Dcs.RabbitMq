using Dcs.Messaging;
using Dcs.Messaging.Common;
using Dcs.Messaging.Grpc;
using Dcs.Messaging.RequestResponse;
using Dcs.Messaging.Resiliency;
using Dcs.Messaging.Serialization;
using Dcs.Messaging.Tcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Dcs.Messaging.Common.DependencyInjection
{
    /// <summary>
    /// Extension methods for wiring messaging sessions into a
    /// <see cref="IServiceCollection"/>. The framework can be used without DI
    /// (just call <c>new TcpMessagingSessionBuilder(...)</c>), but these helpers
    /// keep complexity to a minimum when DI is already in play.
    /// </summary>
    public static class MessagingServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a singleton TCP messaging session and exposes the standard
        /// messaging services (<see cref="IMessagingSessionBuilder"/>,
        /// <see cref="IMessagingService"/>, <see cref="IRequestResponder"/>,
        /// <see cref="IBinarySerializer"/>, <see cref="IMessageFactory"/>,
        /// <see cref="IEndpointDetailsFactory"/>) for resolution.
        /// </summary>
        public static IServiceCollection AddDcsTcpMessaging(
            this IServiceCollection services,
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            TcpSessionOptions sessionOptions)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (endpointDetailsProvider == null)
            {
                throw new ArgumentNullException(nameof(endpointDetailsProvider));
            }
            if (sessionOptions == null)
            {
                throw new ArgumentNullException(nameof(sessionOptions));
            }

            var resiliency = sessionOptions.Resiliency ?? ResiliencyOptions.Default;
            services.TryAddSingleton<IClock>(SystemClock.Instance);
            services.TryAddSingleton<IRetryPolicy>(_ => new PollyRetryPolicy(resiliency));

            services.AddSingleton<TcpMessagingSessionBuilder>(sp => new TcpMessagingSessionBuilder(
                sessionName,
                endpointDetailsProvider,
                sessionOptions,
                sp.GetRequiredService<IRetryPolicy>(),
                sp.GetRequiredService<IClock>()));

            RegisterCommonAbstractions<TcpMessagingSessionBuilder>(services);
            return services;
        }

        /// <summary>
        /// Registers a singleton gRPC messaging session and exposes the standard
        /// messaging services for resolution. See
        /// <see cref="AddDcsTcpMessaging(IServiceCollection, string, IEndpointDetailsProvider, TcpSessionOptions)"/>.
        /// </summary>
        public static IServiceCollection AddDcsGrpcMessaging(
            this IServiceCollection services,
            string sessionName,
            IEndpointDetailsProvider endpointDetailsProvider,
            GrpcSessionOptions sessionOptions)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (endpointDetailsProvider == null)
            {
                throw new ArgumentNullException(nameof(endpointDetailsProvider));
            }
            if (sessionOptions == null)
            {
                throw new ArgumentNullException(nameof(sessionOptions));
            }

            var resiliency = sessionOptions.Resiliency ?? ResiliencyOptions.Default;
            services.TryAddSingleton<IClock>(SystemClock.Instance);
            services.TryAddSingleton<IRetryPolicy>(_ => new PollyRetryPolicy(resiliency));

            services.AddSingleton<GrpcMessagingSessionBuilder>(sp => new GrpcMessagingSessionBuilder(
                sessionName,
                endpointDetailsProvider,
                sessionOptions,
                sp.GetRequiredService<IRetryPolicy>(),
                sp.GetRequiredService<IClock>()));

            RegisterCommonAbstractions<GrpcMessagingSessionBuilder>(services);
            return services;
        }

        /// <summary>
        /// Replaces the default <see cref="ResiliencyOptions"/> / <see cref="IRetryPolicy"/>
        /// registrations. Call before <see cref="AddDcsTcpMessaging(IServiceCollection, string, IEndpointDetailsProvider, TcpSessionOptions)"/>
        /// or <see cref="AddDcsGrpcMessaging(IServiceCollection, string, IEndpointDetailsProvider, GrpcSessionOptions)"/>
        /// to override the defaults.
        /// </summary>
        public static IServiceCollection AddDcsMessagingResiliency(
            this IServiceCollection services,
            ResiliencyOptions options = null,
            IRetryPolicy retryPolicy = null,
            IClock clock = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var effectiveOptions = options ?? ResiliencyOptions.Default;
            services.TryAddSingleton(effectiveOptions);
            services.TryAddSingleton<IClock>(clock ?? SystemClock.Instance);
            services.TryAddSingleton<IRetryPolicy>(retryPolicy ?? new PollyRetryPolicy(effectiveOptions));
            return services;
        }

        private static void RegisterCommonAbstractions<TBuilder>(IServiceCollection services)
            where TBuilder : MessagingSessionBuilderBase
        {
            services.AddSingleton<IMessagingSessionBuilder>(sp => sp.GetRequiredService<TBuilder>());
            services.AddSingleton<IMessagingService>(sp => sp.GetRequiredService<TBuilder>().MessagingService);
            services.AddSingleton<IRequestResponder>(sp => sp.GetRequiredService<TBuilder>().RequestResponder);
            services.AddSingleton<IBinarySerializer>(sp => sp.GetRequiredService<TBuilder>().Serializer);
            services.AddSingleton<IMessageFactory>(sp => sp.GetRequiredService<TBuilder>().MessageFactory);
            services.AddSingleton<IEndpointDetailsFactory>(sp => sp.GetRequiredService<TBuilder>().EndpointDetailsFactory);
            services.AddSingleton<IEndpointDetailsProvider>(sp => sp.GetRequiredService<TBuilder>().EndpointDetailsProvider);
        }
    }
}
