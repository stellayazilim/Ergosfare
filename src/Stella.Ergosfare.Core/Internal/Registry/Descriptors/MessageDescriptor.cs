using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Registry.Descriptors;


/// <summary>
/// Represents a descriptor for a specific message type, holding all associated handlers 
/// and interceptors (pre, main, post, exception, and final).
/// </summary>
/// <remarks>
/// This class categorizes descriptors into direct and indirect collections:
/// - Direct descriptors match the message type exactly.
/// - Indirect descriptors match assignable message types (base types or interfaces).
/// </remarks>
internal class MessageDescriptor(Type messageType) : IMessageDescriptor
{

    // pre interceptors
    private readonly List<IPreInterceptorDescriptor> _preInterceptors = new();
    private readonly List<IPreInterceptorDescriptor> _indirectPreInterceptors = new();

    // main handlers
    private readonly List<IMainHandlerDescriptor> _handlers = new();
    private readonly List<IMainHandlerDescriptor> _indirectHandlers = new();
    
    // post interceptors
    private readonly List<IPostInterceptorDescriptor> _postInterceptors = new();
    private readonly List<IPostInterceptorDescriptor> _indirectPostInterceptors = new();
    
    // exception interceptors
    private readonly List<IExceptionInterceptorDescriptor> _exceptionInterceptors = new();
    private readonly List<IExceptionInterceptorDescriptor> _indirectExceptionInterceptors = new();
    
    // final intercepors
    private readonly List<IFinalInterceptorDescriptor> _finalInterceptors = new();
    private readonly List<IFinalInterceptorDescriptor> _indirectFinalInterceptors = new();
    
    
    /// <summary>
    /// Gets the message type associated with this descriptor.
    /// </summary>
    public Type MessageType { get; } = messageType;
    
    /// <summary>
    /// Gets a value indicating whether the message type is generic.
    /// </summary>
    public bool IsGeneric { get; } = messageType.IsGenericType;

    // pre interceptor
    public IReadOnlyCollection<IPreInterceptorDescriptor> PreInterceptors => _preInterceptors;
    public IReadOnlyCollection<IPreInterceptorDescriptor> IndirectPreInterceptors => _indirectPreInterceptors;
    
    // main handlers
    public IReadOnlyCollection<IMainHandlerDescriptor> Handlers => _handlers;
    public IReadOnlyCollection<IMainHandlerDescriptor> IndirectHandlers => _indirectHandlers;

    // post interceptors
    public IReadOnlyCollection<IPostInterceptorDescriptor> PostInterceptors => _postInterceptors;
    public IReadOnlyCollection<IPostInterceptorDescriptor> IndirectPostInterceptors => _indirectPostInterceptors;
    
    
    // exception interceptors
    public IReadOnlyCollection<IExceptionInterceptorDescriptor> ExceptionInterceptors => _exceptionInterceptors;
    public IReadOnlyCollection<IExceptionInterceptorDescriptor> IndirectExceptionInterceptors => _indirectExceptionInterceptors;
    
    // filan interceptors
    public IReadOnlyCollection<IFinalInterceptorDescriptor> FinalInterceptors => _finalInterceptors;
    public IReadOnlyCollection<IFinalInterceptorDescriptor> IndirectFinalInterceptors => _indirectFinalInterceptors;

    public bool HasInterceptors =>
        _preInterceptors.Count > 0 || _indirectPreInterceptors.Count > 0 ||
        _postInterceptors.Count > 0 || _indirectPostInterceptors.Count > 0 ||
        _exceptionInterceptors.Count > 0 || _indirectExceptionInterceptors.Count > 0 ||
        _finalInterceptors.Count > 0 || _indirectFinalInterceptors.Count > 0;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, HandlerDescriptorCache> _groupCache = new();

    private bool _isDirty = false;
    
    /// <summary>
    /// Adds multiple handler descriptors to this message descriptor.
    /// </summary>
    /// <param name="descriptors">The collection of descriptors to add.</param>
    public void AddDescriptors(IEnumerable<IHandlerDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            AddDescriptorInternal(descriptor);
        }
        SortAll();
    }
    
    
    /// <summary>
    /// Adds a single handler descriptor to this message descriptor.
    /// Direct or indirect placement depends on whether the descriptor's
    /// <see cref="IHandlerDescriptor.MessageType"/> exactly matches or is assignable to the message type.
    /// </summary>
    /// <param name="descriptor">The descriptor to add.</param>
    public void AddDescriptor(IHandlerDescriptor descriptor)
    {
        AddDescriptorInternal(descriptor);
        SortAll();
    }

    private void AddDescriptorInternal(IHandlerDescriptor descriptor)
    {
        _isDirty = true;
        if (MessageType == descriptor.MessageType)
        {
            switch (descriptor)
            {
                case IPreInterceptorDescriptor preInterceptorDescriptor:
                    _preInterceptors.Add(preInterceptorDescriptor);
                    break;
                case IMainHandlerDescriptor mainHandlerDescriptor : 
                    _handlers.Add(mainHandlerDescriptor); 
                    break;
                case IPostInterceptorDescriptor postInterceptorDescriptor:
                    _postInterceptors.Add(postInterceptorDescriptor);
                    break;
                case IExceptionInterceptorDescriptor exceptionInterceptorDescriptor:
                    _exceptionInterceptors.Add(exceptionInterceptorDescriptor);
                    break;
                case IFinalInterceptorDescriptor finalInterceptorDescriptor:
                    _finalInterceptors.Add(finalInterceptorDescriptor);
                    break;
            }
        }
        
        else if (MessageType.IsAssignableTo(descriptor.MessageType))
        {
            switch (descriptor)
            {
                case IPreInterceptorDescriptor preInterceptorDescriptor:
                    _indirectPreInterceptors.Add(preInterceptorDescriptor);
                    break;
                case IMainHandlerDescriptor mainHandlerDescriptor:
                    _indirectHandlers.Add(mainHandlerDescriptor);
                    break;
                case IPostInterceptorDescriptor postInterceptorDescriptor:
                    _indirectPostInterceptors.Add(postInterceptorDescriptor);
                    break;
                case IExceptionInterceptorDescriptor exceptionInterceptorDescriptor:
                    _indirectExceptionInterceptors.Add(exceptionInterceptorDescriptor);
                    break;
                case IFinalInterceptorDescriptor finalInterceptorDescriptor:
                    _indirectFinalInterceptors.Add(finalInterceptorDescriptor);
                    break;
            }
        }
    }

    private void SortAll()
    {
        if (!_isDirty) return;
        SortList(_preInterceptors);
        SortList(_indirectPreInterceptors);
        SortList(_handlers);
        SortList(_indirectHandlers);
        SortList(_postInterceptors);
        SortList(_indirectPostInterceptors);
        SortList(_exceptionInterceptors);
        SortList(_indirectExceptionInterceptors);
        SortList(_finalInterceptors);
        SortList(_indirectFinalInterceptors);
        _groupCache.Clear();
        _isDirty = false;
    }

    internal HandlerDescriptorCache GetCachedDescriptors(IEnumerable<string> groups)
    {
        var groupKey = string.Join('|', groups.OrderBy(g => g));
        return _groupCache.GetOrAdd(groupKey, _ => BuildCache(groups));
    }

    private HandlerDescriptorCache BuildCache(IEnumerable<string> groups)
    {
        var groupList = groups.ToList();
        return new HandlerDescriptorCache
        {
            PreInterceptors = FilterByGroup(_preInterceptors, groupList),
            IndirectPreInterceptors = FilterByGroup(_indirectPreInterceptors, groupList),
            Handlers = FilterByGroup(_handlers, groupList),
            IndirectHandlers = FilterByGroup(_indirectHandlers, groupList),
            PostInterceptors = FilterByGroup(_postInterceptors, groupList),
            IndirectPostInterceptors = FilterByGroup(_indirectPostInterceptors, groupList),
            ExceptionInterceptors = FilterByGroup(_exceptionInterceptors, groupList),
            IndirectExceptionInterceptors = FilterByGroup(_indirectExceptionInterceptors, groupList),
            FinalInterceptors = FilterByGroup(_finalInterceptors, groupList),
            IndirectFinalInterceptors = FilterByGroup(_indirectFinalInterceptors, groupList)
        };
    }

    private static TDescriptor[] FilterByGroup<TDescriptor>(List<TDescriptor> descriptors, List<string> groups) where TDescriptor : IHandlerDescriptor
    {
        var result = new List<TDescriptor>(descriptors.Count);
        foreach (var d in descriptors)
        {
            if (IsInGroup(d.Groups, groups))
            {
                result.Add(d);
            }
        }
        return result.ToArray();
    }

    private static bool IsInGroup(IReadOnlyCollection<string> handlerGroups, List<string> groups)
    {
        foreach (var g in groups)
        {
            foreach (var hg in handlerGroups)
            {
                if (g == hg) return true;
            }
        }
        return false;
    }

    internal class HandlerDescriptorCache
    {
        public required IPreInterceptorDescriptor[] PreInterceptors { get; init; }
        public required IPreInterceptorDescriptor[] IndirectPreInterceptors { get; init; }
        public required IMainHandlerDescriptor[] Handlers { get; init; }
        public required IMainHandlerDescriptor[] IndirectHandlers { get; init; }
        public required IPostInterceptorDescriptor[] PostInterceptors { get; init; }
        public required IPostInterceptorDescriptor[] IndirectPostInterceptors { get; init; }
        public required IExceptionInterceptorDescriptor[] ExceptionInterceptors { get; init; }
        public required IExceptionInterceptorDescriptor[] IndirectExceptionInterceptors { get; init; }
        public required IFinalInterceptorDescriptor[] FinalInterceptors { get; init; }
        public required IFinalInterceptorDescriptor[] IndirectFinalInterceptors { get; init; }

        public bool HasInterceptors =>
            PreInterceptors.Length > 0 || IndirectPreInterceptors.Length > 0 ||
            PostInterceptors.Length > 0 || IndirectPostInterceptors.Length > 0 ||
            ExceptionInterceptors.Length > 0 || IndirectExceptionInterceptors.Length > 0 ||
            FinalInterceptors.Length > 0 || IndirectFinalInterceptors.Length > 0;
    }

    private static void SortList<T>(List<T> list) where T : IHandlerDescriptor
    {
        if (list.Count <= 1) return;

        list.Sort((x, y) =>
        {
            // Weight descending
            int result = y.Weight.CompareTo(x.Weight);
            if (result != 0) return result;

            // FullName ascending
            return string.Compare(x.HandlerType.FullName, y.HandlerType.FullName, StringComparison.Ordinal);
        });
    }
}