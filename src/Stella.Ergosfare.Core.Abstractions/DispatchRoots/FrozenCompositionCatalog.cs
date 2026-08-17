using System.Collections.Concurrent;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;

/// <summary>
/// One container's view of the compiled composition table: the pipelines it can run,
/// narrowed to the participants it actually registered.
/// </summary>
/// <remarks>
/// <para>
/// The table itself is process-wide and holds every composition the whole compiled closure
/// produced. An application typically registers only part of that, so this type records
/// what registration named and narrows each composition to it. A participant this container
/// never registered is not in its pipeline — and could not be resolved from it anyway.
/// </para>
/// <para>
/// Selection is the whole answer, including when it is empty: a container that registered
/// nothing runs nothing, and a message with no registered handler has no pipeline.
/// </para>
/// </remarks>
public sealed class FrozenCompositionCatalog
{
    /// <summary>
    /// The types registration named. Registration finishes before the container is built,
    /// so this set is written only during setup.
    /// </summary>
    private readonly HashSet<Type> _selected = [];

    /// <summary>
    /// The narrowed composition of each runtime message type, misses included, cached on
    /// first dispatch of that type.
    /// </summary>
    private readonly ConcurrentDictionary<Type, FrozenComposition?> _projected = new();

    /// <summary>
    /// Compositions handed to this container directly rather than compiled into the
    /// process-wide table. Consulted first and never narrowed.
    /// </summary>
    private readonly ConcurrentDictionary<Type, FrozenComposition> _local = new();

    /// <summary>
    /// Adds a composition this container serves itself, overriding the compiled table for
    /// the same message type.
    /// </summary>
    /// <param name="composition">The composition to serve. Cannot be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="composition"/> is <c>null</c>.</exception>
    /// <remarks>
    /// A local composition is matched by message type exactly, without the ancestor walk
    /// the compiled table gets, and is taken whole rather than narrowed by selection —
    /// supplying one states the pipeline instead of choosing from it. Add it before the
    /// message is first dispatched, since compositions are cached per type.
    /// </remarks>
    public void Add(FrozenComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var messageType = composition.MessageType;

        _local[messageType.IsGenericType ? messageType.GetGenericTypeDefinition() : messageType] = composition;
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
        => _selected.Contains(handlerType)
           || (handlerType.IsGenericType
               && !handlerType.IsGenericTypeDefinition
               && _selected.Contains(handlerType.GetGenericTypeDefinition()));

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
            foreach (var participantType in participantTypes)
            {
                _selected.Add(participantType);
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

    /// <summary>
    /// The participant types this container both registered and can run: its selection
    /// intersected with the participants the compiled table names, plus every participant
    /// of any composition handed over directly.
    /// </summary>
    /// <returns>The participant types to register as services.</returns>
    /// <remarks>
    /// <para>
    /// This is what container registration derives its service registrations from.
    /// Selection alone would not do, since it also holds message types — things to
    /// dispatch, not services to resolve.
    /// </para>
    /// <para>
    /// Intended for setup, before any composition is cached: it reads the whole table once.
    /// Every type returned came from <see cref="FrozenParticipant.HandlerType"/>, so its
    /// public constructors survive trimming; the sequence itself cannot carry that
    /// annotation, which is why the caller registering these suppresses the dataflow
    /// warning instead of restating it.
    /// </para>
    /// </remarks>
    public IEnumerable<Type> SelectedParticipants()
    {
        var participants = new HashSet<Type>();

        lock (_selected)
        {
            foreach (var composition in GeneratedDispatchRoots.FrozenCompositionEntries)
            {
                Collect(composition, participants, narrow: true);
            }

            // Local compositions state the pipeline rather than choose from it, so their
            // participants are taken whole — the same rule Find applies to them.
            foreach (var composition in _local.Values)
            {
                Collect(composition, participants, narrow: false);
            }
        }

        return participants;

        void Collect(FrozenComposition composition, HashSet<Type> into, bool narrow)
        {
            Add(composition.HandlerRows, into, narrow);
            Add(composition.IndirectHandlerRows, into, narrow);
            Add(composition.PreInterceptorRows, into, narrow);
            Add(composition.IndirectPreInterceptorRows, into, narrow);
            Add(composition.PostInterceptorRows, into, narrow);
            Add(composition.IndirectPostInterceptorRows, into, narrow);
            Add(composition.ExceptionInterceptorRows, into, narrow);
            Add(composition.IndirectExceptionInterceptorRows, into, narrow);
            Add(composition.FinalInterceptorRows, into, narrow);
            Add(composition.IndirectFinalInterceptorRows, into, narrow);
        }

        // ReSharper disable once LocalFunctionHidesMethod
        void Add(FrozenParticipant[] segment, HashSet<Type> into, bool narrow)
        {
            foreach (var participant in segment)
            {
                if (!narrow || IsSelected(participant.HandlerType))
                {
                    into.Add(participant.HandlerType);
                }
            }
        }
    }

    /// <summary>
    /// Returns the composition serving <paramref name="messageType"/> in this container,
    /// narrowed to the participants it registered.
    /// </summary>
    /// <param name="messageType">The dispatched message's runtime type. Cannot be <c>null</c>.</param>
    /// <returns>
    /// The composition, or <c>null</c> when nothing serves the type — which is what tells
    /// the caller there is no handler.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="messageType"/> is <c>null</c>.</exception>
    public FrozenComposition? Find(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        if (_projected.TryGetValue(messageType, out var cached))
        {
            return cached;
        }

        FrozenComposition? projection;

        if (_local.TryGetValue(
                messageType.IsGenericType ? messageType.GetGenericTypeDefinition() : messageType, out var local))
        {
            projection = local;
        }
        else
        {
            var composition = GeneratedDispatchRoots.FindFrozenComposition(messageType);
            projection = composition is null ? null : Project(composition);
        }

        _projected[messageType] = projection;

        return projection;
    }

    /// <summary>
    /// Narrows a composition to the rows this container registered.
    /// </summary>
    /// <param name="composition">The composition from the compiled table.</param>
    /// <returns>
    /// The narrowed composition, or <paramref name="composition"/> itself when every row
    /// survives — the common case of one container registering the whole closure, which
    /// then allocates nothing and keeps composition identity stable.
    /// </returns>
    private FrozenComposition Project(FrozenComposition composition)
    {
        lock (_selected)
        {
            var handlers = Select(composition.HandlerRows);
            var indirectHandlers = Select(composition.IndirectHandlerRows);
            var pre = Select(composition.PreInterceptorRows);
            var indirectPre = Select(composition.IndirectPreInterceptorRows);
            var post = Select(composition.PostInterceptorRows);
            var indirectPost = Select(composition.IndirectPostInterceptorRows);
            var exception = Select(composition.ExceptionInterceptorRows);
            var indirectException = Select(composition.IndirectExceptionInterceptorRows);
            var final = Select(composition.FinalInterceptorRows);
            var indirectFinal = Select(composition.IndirectFinalInterceptorRows);

            if (ReferenceEquals(handlers, composition.HandlerRows)
                && ReferenceEquals(indirectHandlers, composition.IndirectHandlerRows)
                && ReferenceEquals(pre, composition.PreInterceptorRows)
                && ReferenceEquals(indirectPre, composition.IndirectPreInterceptorRows)
                && ReferenceEquals(post, composition.PostInterceptorRows)
                && ReferenceEquals(indirectPost, composition.IndirectPostInterceptorRows)
                && ReferenceEquals(exception, composition.ExceptionInterceptorRows)
                && ReferenceEquals(indirectException, composition.IndirectExceptionInterceptorRows)
                && ReferenceEquals(final, composition.FinalInterceptorRows)
                && ReferenceEquals(indirectFinal, composition.IndirectFinalInterceptorRows))
            {
                return composition;
            }

            return new FrozenComposition(
                composition.MessageType,
                handlers, indirectHandlers,
                pre, indirectPre,
                post, indirectPost,
                exception, indirectException,
                final, indirectFinal);
        }
    }

    /// <summary>
    /// Returns the registered rows of one segment, order preserved.
    /// </summary>
    /// <param name="segment">The segment to narrow.</param>
    /// <returns>
    /// The segment itself when every row is registered, so the caller can detect an
    /// unchanged composition by reference.
    /// </returns>
    private FrozenParticipant[] Select(FrozenParticipant[] segment)
    {
        var count = 0;

        foreach (var participant in segment)
        {
            if (IsSelected(participant.HandlerType))
            {
                count++;
            }
        }

        if (count == segment.Length)
        {
            return segment;
        }

        if (count == 0)
        {
            return [];
        }

        var selected = new FrozenParticipant[count];
        var index = 0;

        foreach (var participant in segment)
        {
            if (IsSelected(participant.HandlerType))
            {
                selected[index++] = participant;
            }
        }

        return selected;
    }
}
