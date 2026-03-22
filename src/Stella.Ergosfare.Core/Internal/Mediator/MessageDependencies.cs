using Stella.Ergosfare.Contracts.Attributes;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Internal.Mediator;


/// <summary>
/// Provides immediate resolution and access to all handler dependencies for a given message type,
/// including pre/post interceptors, main handlers, exception interceptors, and final interceptors.
/// </summary>
internal sealed class MessageDependencies : IMessageDependencies
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, Type), Type> GenericTypeCache = new();
    private static readonly string[] DefaultGroupArray = [ GroupAttribute.DefaultGroupName ];

    private readonly Type _messageType;
    private readonly IServiceProvider _serviceProvider;
    private readonly Stella.Ergosfare.Core.Internal.Registry.Descriptors.MessageDescriptor.HandlerDescriptorCache _cached;

    public bool HasInterceptors => _cached.HasInterceptors;
    
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> PreInterceptors { get; }
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> IndirectPreInterceptors { get; }
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; }
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers { get; }
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> PostInterceptors { get; }
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> IndirectPostInterceptors { get; }
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> ExceptionInterceptors { get; }
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> IndirectExceptionInterceptors { get; }
    public ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> FinalInterceptors { get; }
    public ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> IndirectFinalInterceptors { get; }
  
    public MessageDependencies(Type messageType,
        IMessageDescriptor descriptor,
        IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        _messageType = messageType;
        _serviceProvider = serviceProvider;

        // Minimize allocations: check if groups is already a collection or array
        if (groups is IReadOnlyCollection<string> coll && coll.Count > 0)
        {
            _cached = ((Stella.Ergosfare.Core.Internal.Registry.Descriptors.MessageDescriptor)descriptor).GetCachedDescriptors(coll);
        }
        else if (groups is string[] { Length: > 0 } arr)
        {
            _cached = ((Stella.Ergosfare.Core.Internal.Registry.Descriptors.MessageDescriptor)descriptor).GetCachedDescriptors(arr);
        }
        else
        {
            var list = groups.ToList();
            var finalGroups = list.Count == 0 ? (IEnumerable<string>)DefaultGroupArray : list;
            _cached = ((Stella.Ergosfare.Core.Internal.Registry.Descriptors.MessageDescriptor)descriptor).GetCachedDescriptors(finalGroups);
        }

        PreInterceptors = ResolveHandlers(_cached.PreInterceptors, t => (IPreInterceptor)_serviceProvider.GetRequiredService(t));
        IndirectPreInterceptors = ResolveHandlers(_cached.IndirectPreInterceptors, t => (IPreInterceptor)_serviceProvider.GetRequiredService(t));
        Handlers = ResolveHandlers(_cached.Handlers, t => (IHandler)_serviceProvider.GetRequiredService(t));
        IndirectHandlers = ResolveHandlers(_cached.IndirectHandlers, t => (IHandler)_serviceProvider.GetRequiredService(t));
        PostInterceptors = ResolveHandlers(_cached.PostInterceptors, t => (IPostInterceptor)_serviceProvider.GetRequiredService(t));
        IndirectPostInterceptors = ResolveHandlers(_cached.IndirectPostInterceptors, t => (IPostInterceptor)_serviceProvider.GetRequiredService(t));
        ExceptionInterceptors = ResolveHandlers(_cached.ExceptionInterceptors, t => (IExceptionInterceptor)_serviceProvider.GetRequiredService(t));
        IndirectExceptionInterceptors = ResolveHandlers(_cached.IndirectExceptionInterceptors, t => (IExceptionInterceptor)_serviceProvider.GetRequiredService(t));
        FinalInterceptors = ResolveHandlers(_cached.FinalInterceptors, t => (IFinalInterceptor)_serviceProvider.GetRequiredService(t));
        IndirectFinalInterceptors = ResolveHandlers(_cached.IndirectFinalInterceptors, t => (IFinalInterceptor)_serviceProvider.GetRequiredService(t));
    }

    private ILazyHandlerCollection<THandler, TDescriptor> ResolveHandlers<THandler, TDescriptor>(
            TDescriptor[] descriptors,
            Func<Type, THandler> resolveFunc ) where TDescriptor : IHandlerDescriptor
    {
        if (descriptors.Length == 0)
        {
            return EmptyLazyHandlerCollection<THandler, TDescriptor>.Instance;
        }


        var result = new ILazyHandler<THandler, TDescriptor>[descriptors.Length];

        for (int i = 0; i < descriptors.Length; i++)
        {
            var d = descriptors[i];
            result[i] = new LazyHandler<THandler, TDescriptor>
            {
                Handler = resolveFunc(GetHandlerType(d)),
                Descriptor = d
            };
        }

        return new HandlerArrayCollection<THandler, TDescriptor>(result);
    }

    private static class EmptyLazyHandlerCollection<THandler, TDescriptor> where TDescriptor : IHandlerDescriptor
    {
        public static readonly ILazyHandlerCollection<THandler, TDescriptor> Instance = new HandlerArrayCollection<THandler, TDescriptor>([]);
    }

    private Type GetHandlerType(IHandlerDescriptor descriptor)
    {
        var handlerType = descriptor.HandlerType;

        if (descriptor.MessageType.IsGenericType && handlerType.IsGenericTypeDefinition)
        {
            return GenericTypeCache.GetOrAdd((handlerType, _messageType), _ =>
                handlerType.MakeGenericType(_messageType.GetGenericArguments()));
        }
        return handlerType;
    }
}
