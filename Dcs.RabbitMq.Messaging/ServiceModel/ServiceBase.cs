using Dcs.RabbitMq.Messaging.Commanding;
using Dcs.RabbitMq.Messaging.Messaging;
using Dcs.RabbitMq.Messaging.RequestResponse;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;

namespace Dcs.RabbitMq.Messaging.ServiceModel
{
    public class CommandWrapper<T>
    {
        private readonly string _senderSessionId;
        private readonly T _command;

        public CommandWrapper(
            string senderSessionId,
            T command)
        {
            _senderSessionId = senderSessionId;
            _command = command;
        }

        public string SenderSessionId
        {
            get { return _senderSessionId; }
        }

        public T Command
        {
            get { return _command; }
        }
    }

    public abstract class ServiceBase<T> : IService
    {
        private readonly IRequestResponder _requestResponder;
        private readonly ICommandListener _commandListener;
        private IEndpointDetailsProvider _endpointDetailsProvider;
        private readonly IList<IDisposable> _subscriptions;
        private bool _disposed;
        private bool _startCalled = false;

        public IEndpointDetailsProvider EndpointDetailsProvider
        {
            get { return _endpointDetailsProvider; }
            set
            {
                if (_startCalled)
                {
                    throw new InvalidOperationException("Cannot set the EndpontDetailsProvider after the service has started");
                }
                _endpointDetailsProvider = value;
            }
        }

        protected ServiceBase(
            IRequestResponder requestResponder,
            ICommandListener commandListener,
            IEndpointDetailsProvider endpointDetailsProvider)
        {
            _requestResponder = requestResponder;
            _commandListener = commandListener;
            _endpointDetailsProvider = endpointDetailsProvider;

            _subscriptions = new List<IDisposable>();
        }

        public virtual void Start()
        {
            _startCalled = true;
            var interfaceType = typeof(T);
            var methodInfos = interfaceType.GetMethods();
            foreach (var methodInfo in methodInfos)
            {
                ServiceMethod(methodInfo);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                OnDispose(_disposed);
                foreach (var subscription in _subscriptions)
                {
                    subscription.Dispose();
                }

                _disposed = true;
            }
        }

        protected virtual void OnDispose(bool disposed) { }

        private void ServiceMethod(MethodInfo interfaceMethodInfo)
        {
            var thisType = GetType();
            var parameterTypes =
                interfaceMethodInfo.GetParameters().Select(pi => pi.ParameterType).ToArray();
            var concreteMethodInfo = thisType.GetMethod(interfaceMethodInfo.Name, parameterTypes);
            var inputType = parameterTypes.Single();
            var outputType = interfaceMethodInfo.ReturnType;
            var operationAttribute = interfaceMethodInfo.GetCustomAttributes(typeof(OperationAttribute), true)
                .Cast<OperationAttribute>()
                .Single();

            if (operationAttribute is RequestResponseOperationAttribute)
            {
                var requestResponseOperationAttribute =
                    (RequestResponseOperationAttribute)operationAttribute;

                if (outputType.IsGenericType && outputType.GetGenericTypeDefinition() == typeof(IObservable<>))
                {
                    ServiceAsyncRequestResponseMethod(
                        inputType,
                        outputType.GetGenericArguments()[0],
                        concreteMethodInfo,
                        requestResponseOperationAttribute);
                }
                else
                {
                    ServiceRequestResponseMethod(
                        inputType, outputType,
                        concreteMethodInfo,
                        requestResponseOperationAttribute);
                }
            }
            else if (operationAttribute is CommandOperationAttribute)
            {
                var commandAttribute = (CommandOperationAttribute)operationAttribute;
                ServiceCommandMethod(inputType, concreteMethodInfo, commandAttribute);
            }
            else
            {
                throw new InvalidOperationException("Unsupported operation type");
            }
        }

        private void ServiceRequestResponseMethod(
            Type requestType, Type responseType,
            MethodInfo concreteMethodInfo,
            RequestResponseOperationAttribute requestResponseAttribute)
        {
            var thisType = GetType();
            var openMethodInfo = GetGenericMethod(thisType, "ServiceRequestResponseMethodGeneric");
            var typeArguments = new[] { requestType, responseType };
            var closedMethodInfo = openMethodInfo.MakeGenericMethod(typeArguments);
            var parameters = new object[] { concreteMethodInfo, requestResponseAttribute };
            closedMethodInfo.Invoke(this, parameters);
        }

        private void ServiceAsyncRequestResponseMethod(
            Type requestType, Type responseType,
            MethodInfo concreteMethodInfo,
            RequestResponseOperationAttribute requestResponseAttribute)
        {
            var thisType = GetType();
            var openMethodInfo = GetGenericMethod(thisType, "ServiceAsyncRequestResponseMethodGeneric");
            var typeArguments = new[] { requestType, responseType };
            var closedMethodInfo = openMethodInfo.MakeGenericMethod(typeArguments);
            var parameters = new object[] { concreteMethodInfo, requestResponseAttribute };
            closedMethodInfo.Invoke(this, parameters);
        }

        // TODO: Make work when ServiceRequestResponseMethodGeneric is private
        private static MethodInfo GetGenericMethod(Type thisType, string methodName)
        {
            MemberInfo[] mis = thisType.GetMember(methodName + "*", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod);
            foreach (var memberInfo in mis)
            {
                if (memberInfo.MemberType == MemberTypes.Method)
                {
                    var methodInfo = (MethodInfo)memberInfo;
                    return methodInfo;
                }
            }

            // Did not find a matching method
            Debug.WriteLine("No generic methods matching " + methodName);
            return null;
        }

        // ReSharper disable UnusedMember.Local
        internal void ServiceRequestResponseMethodGeneric<TRequest, TResponse>(
            MethodInfo concreteMethodInfo,
            RequestResponseOperationAttribute requestResponseAttribute)
        // ReSharper restore UnusedMember.Local
        {
            var requestEndpointDetails = _endpointDetailsProvider
                .GetEndpointDetails(requestResponseAttribute.RequestEndpointKey);
            var responseEndpointDetails = _endpointDetailsProvider
                .GetEndpointDetails(requestResponseAttribute.ResponseEndpointKey);
            var execute =
                (Func<TRequest, TResponse>)Delegate.CreateDelegate(
                    typeof(Func<TRequest, TResponse>),
                    this, concreteMethodInfo, true);

            var subscription =
                _requestResponder
                .GetRespondableRequestStream<TRequest, TResponse>(
                    requestEndpointDetails,
                    responseEndpointDetails)
                .Subscribe(respondableRequest =>
                {
                    RequestContext.Current =
                        new RequestContext(respondableRequest.SenderSessionId);
                    try
                    {
                        var response = execute(respondableRequest.Request);
                        respondableRequest.Respond(response);
                    }
                    catch (Exception error)
                    {
                        respondableRequest.Respond(error);
                    }
                    finally
                    {
                        RequestContext.Current = null;
                    }
                });
            _subscriptions.Add(subscription);
        }

        // ReSharper disable UnusedMember.Local
        internal void ServiceAsyncRequestResponseMethodGeneric<TRequest, TResponse>(
            MethodInfo concreteMethodInfo,
            RequestResponseOperationAttribute requestResponseAttribute)
        // ReSharper restore UnusedMember.Local
        {
            var requestEndpointDetails = _endpointDetailsProvider
                .GetEndpointDetails(requestResponseAttribute.RequestEndpointKey);
            var responseEndpointDetails = _endpointDetailsProvider
                .GetEndpointDetails(requestResponseAttribute.ResponseEndpointKey);
            var execute =
                (Func<TRequest, IObservable<TResponse>>)Delegate.CreateDelegate(
                    typeof(Func<TRequest, IObservable<TResponse>>),
                    this, concreteMethodInfo, true);

            var subscription = _requestResponder
                .GetRespondableRequestStream<TRequest, TResponse>(
                    requestEndpointDetails,
                    responseEndpointDetails)
                .Subscribe(respondableRequest =>
                {
                    RequestContext.Current =
                        new RequestContext(respondableRequest.SenderSessionId);

                    try
                    {
                        var response = execute(respondableRequest.Request);
                        response.Take(1).Subscribe(
                            respondableRequest.Respond,
                            respondableRequest.Respond);
                    }
                    catch (Exception error)
                    {
                        respondableRequest.Respond(error);
                    }
                    finally
                    {
                        RequestContext.Current = null;
                    }
                });
            _subscriptions.Add(subscription);
        }

        private void ServiceCommandMethod(
            Type commandType,
            MethodInfo concreteMethodInfo,
            CommandOperationAttribute commandAttribute)
        {
            var thisType = GetType();
            var openMethodInfo = thisType.GetMethod("ServiceCommandMethodGeneric", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod);
            var typeArguments = new[] { commandType };
            var closedMethodInfo = openMethodInfo.MakeGenericMethod(typeArguments);
            var parameters = new object[] { concreteMethodInfo, commandAttribute };
            closedMethodInfo.Invoke(this, parameters);
        }

        // ReSharper disable UnusedMember.Local
        internal void ServiceCommandMethodGeneric<TCommand>(
            MethodInfo concreteMethodInfo,
            CommandOperationAttribute commandAttribute)
        // ReSharper restore UnusedMember.Local
        {
            var commandEndpointDetails = _endpointDetailsProvider
                .GetEndpointDetails(commandAttribute.CommandEndpointKey);
            var execute =
                (Func<TCommand, IObservable<Unit>>)Delegate.CreateDelegate(
                    typeof(Func<TCommand, IObservable<Unit>>),
                    this, concreteMethodInfo, true);

            var subscription =
                _commandListener
                .GetCommandStream<TCommand>(commandEndpointDetails)
                .Subscribe(command =>
                {
                    RequestContext.Current = new RequestContext(command.SenderSessionId);
                    try
                    {
                        execute(command.Command);
                    }
                    catch (Exception)
                    {
                        throw new NotImplementedException();
                    }
                    finally
                    {
                        RequestContext.Current = null;
                    }
                });
            _subscriptions.Add(subscription);
        }
    }
}
