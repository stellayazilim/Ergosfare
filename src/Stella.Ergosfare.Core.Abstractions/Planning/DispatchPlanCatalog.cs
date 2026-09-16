using System.Collections.Concurrent;

namespace Stella.Ergosfare.Core.Abstractions.Planning;

/// <summary>
/// A container's runtime catalog of selected registrations and executable generated plans.
/// </summary>
/// <remarks>
/// <para>
/// During engine initialization, registration selection is compared with generated plan
/// descriptors. Compatible plans are stored by reference and looked up during dispatch.
/// This catalog does not construct, wrap or rewrite an executable plan.
/// </para>
/// <para>
/// Selection is the whole answer, including when it is empty: a container that registered
/// nothing runs nothing, and a message with no registered handler has no pipeline.
/// </para>
/// </remarks>
public sealed class DispatchPlanCatalog
{
    private bool _sealed;

    internal void Seal()
    {
        lock (_selected) _sealed = true;
    }

    private void EnsureConfiguring()
    {
        if (_sealed)
            throw new InvalidOperationException("Dispatch registrations are fixed after initialization. Select participants at compile time with AddGenerated or Register<T>.");
    }
    // The selected entries refer to the original generated plan instances. There is no
    // separate admission lookup, executor wrapper, or dispatch-time cache population.
    private readonly Dictionary<(Type Message, Type? Result, byte Kind, StagedPlans.PlanGroupKey Groups),
        StagedPlans.ICompiledPlan?> _executionPlans = new();

    internal void BindPlan(Type message, Type? result, byte kind, IReadOnlyList<string> groups,
        StagedPlans.ICompiledPlan plan, bool selected)
        => _executionPlans[(message, result, kind, new StagedPlans.PlanGroupKey(groups, plan.FilterGroups is not null))]
            = selected ? plan : null;

    internal StagedPlans.ICompiledPlan? FindPlan(Type message, Type? result, byte kind, IReadOnlyList<string> groups)
    {
        // A present but incompatible exact entry must not silently fall through to a
        // different filtering plan. Its diagnostic is produced only on the cold path.
        if (_executionPlans.TryGetValue((message, result, kind, new StagedPlans.PlanGroupKey(groups)), out var plan))
            return plan;
        return _executionPlans.TryGetValue((message, result, kind, StagedPlans.PlanGroupKey.Filtering), out plan)
            ? plan : null;
    }
    /// <summary>
    /// The types registration named. Registration finishes before the container is built,
    /// so this set is written only during setup.
    /// </summary>
    private readonly HashSet<Type> _selected = [];

    /// <summary>
    /// Compositions handed to this container directly rather than compiled into the
    /// process-wide table. Consulted first and never narrowed.
    /// </summary>
    private readonly ConcurrentDictionary<Type, PipelineDescriptor> _local = new();

    /// <summary>
    /// Adds a composition this container serves itself, overriding the compiled table for
    /// the same message type.
    /// </summary>
    /// <param name="composition">The composition to serve. Cannot be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="composition"/> is <c>null</c>.</exception>
    /// <remarks>
    /// A local composition is matched by message type exactly, and is taken whole rather than narrowed by selection —
    /// supplying one states the pipeline instead of choosing from it. Add it during configuration; startup validates it before binding executable plans.
    /// </remarks>
    public void Add(PipelineDescriptor composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var messageType = composition.MessageType;

        lock (_selected)
        {
            EnsureConfiguring();
            _local[messageType] = composition;
        }
    }

    /// <summary>
    /// Reports whether this container registered a participant.
    /// </summary>
    /// <param name="handlerType">The participant type from a composition row.</param>
    /// <returns><c>true</c> when the participant was registered.</returns>
    /// <remarks>
    /// Registering an open generic definition also selects the closed forms the generator
    /// produced from it: a caller writes <c>Register(typeof(ValidateCommands&lt;&gt;))</c>
    /// because that is the definition's only name, while the composition rows name
    /// <c>ValidateCommands&lt;RegisterUser&gt;</c> and its siblings.
    /// </remarks>
    private bool IsSelected(Type handlerType)
        => _selected.Contains(handlerType);

    // Descriptor inspection does not project a composition or construct participant arrays.
    internal PipelineDescriptor? ReadDescriptor(Type messageType, out bool local)
    {
        local = _local.TryGetValue(messageType, out var descriptor);
        return local ? descriptor : GeneratedPlanRegistry.FindPipelineDescriptor(messageType);
    }

    internal bool IncludesParticipant(Type participantType) => IsSelected(participantType);

    /// <summary>
    /// Records that this container registered <paramref name="participantType"/>.
    /// </summary>
    /// <param name="participantType">The registered type. Cannot be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="participantType"/> is <c>null</c>.</exception>
    /// <remarks>Selection is a union, so repeated and overlapping calls are safe.</remarks>
    public void Select(Type participantType)
    {
        ArgumentNullException.ThrowIfNull(participantType);

        lock (_selected)
        {
            EnsureConfiguring();
            if (GeneratedPlanRegistry.ExpandSelection(participantType) is { } closedTypes)
                foreach (var closed in closedTypes) _selected.Add(closed);
            else
                _selected.Add(participantType);
        }
    }

    /// <summary>
    /// Records a batch of registered types; see <see cref="Select(Type)"/>.
    /// </summary>
    /// <param name="participantTypes">The registered types. Cannot be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="participantTypes"/> is <c>null</c>.</exception>
    public void Select(IEnumerable<Type> participantTypes)
    {
        ArgumentNullException.ThrowIfNull(participantTypes);

        lock (_selected)
        {
            EnsureConfiguring();
            foreach (var participantType in participantTypes)
            {
                Select(participantType);
            }
        }
    }

    /// <summary>
    /// Everything registration has named so far — message types and participants alike, in
    /// no particular order.
    /// </summary>
    /// <remarks>
    /// This is the raw selection. For the part that names a pipeline participant, use
    /// <see cref="SelectedParticipants"/>.
    /// </remarks>
    public IReadOnlyCollection<Type> Selections
    {
        get
        {
            lock (_selected)
            {
                return [.. _selected];
            }
        }
    }

    /// <summary>Enumerates selected participants whose service registration was generated.</summary>
    /// <remarks>No descriptor traversal or runtime type classification is performed.</remarks>
    public IEnumerable<Type> SelectedParticipants()
        => _selected.Where(GeneratedPlanRegistry.IsParticipant);

}
