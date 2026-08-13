using System.Collections.Concurrent;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;

/// <summary>
/// One container's view of the process-wide frozen composition table: the table is
/// compiled once for the whole closure, but which of its participant rows an application
/// actually runs is that application's own choice — the constructs it registered.
/// </summary>
/// <remarks>
/// The table itself lives in <see cref="GeneratedDispatchRoots"/> — process-wide, appended
/// at load time, never per container. This type adds the selection layer the message
/// registry used to provide implicitly by only ever containing what someone registered.
/// Registration names its participants (the generated registration code passes them
/// explicitly, a hand-written <c>Register&lt;T&gt;()</c> names one), so selection is
/// recorded type by type and no discovery-key pattern has to survive into the runtime:
/// a row whose participant this container never registered is not part of its pipeline,
/// and asking the container to resolve it would fail.
/// <para>
/// Selection is the whole answer, including when it is empty: a container that registered
/// nothing runs nothing, and a message it never registered a handler for has no pipeline.
/// Admitting the unfiltered table instead would hand every application every participant
/// the closure happens to compile — including ones it cannot resolve.
/// </para>
/// </remarks>
public sealed class FrozenCompositionCatalog
{
    /// <summary>
    /// The participant types this container registered. Registration is complete before
    /// the container is built, so the set is only written during setup.
    /// </summary>
    private readonly HashSet<Type> _selected = [];

    /// <summary>
    /// The projection of each runtime message type, misses included. Populated on first
    /// dispatch of a type and never invalidated — see the selection lifetime above.
    /// </summary>
    private readonly ConcurrentDictionary<Type, FrozenComposition?> _projected = new();

    /// <summary>
    /// Compositions supplied to this container directly rather than compiled into the
    /// process-wide table. Consulted first, and never narrowed by selection: a caller
    /// handing over a composition is stating the pipeline, not choosing from one.
    /// </summary>
    private readonly ConcurrentDictionary<Type, FrozenComposition> _local = new();

    /// <summary>
    /// Adds a composition this container serves itself, overriding whatever the
    /// process-wide table holds for the same message type. Local entries are matched by
    /// message type exactly — the ancestor ladder belongs to the compiled table — and must
    /// be added before the message is first dispatched, since projections are cached per
    /// type.
    /// </summary>
    public void Add(FrozenComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var messageType = composition.MessageType;

        _local[messageType.IsGenericType ? messageType.GetGenericTypeDefinition() : messageType] = composition;
    }

    /// <summary>
    /// Whether this container registered a participant. Selecting an open generic definition
    /// selects the closed forms the generator monomorphized from it: the caller writes
    /// <c>Register(typeof(ValidateCommands&lt;&gt;))</c> because that is the only name the
    /// definition has, while the composition rows name <c>ValidateCommands&lt;RegisterUser&gt;</c>
    /// and its siblings. Matching only by exact type would let a registration select nothing.
    /// </summary>
    private bool IsSelected(Type handlerType)
        => _selected.Contains(handlerType)
           || (handlerType.IsGenericType
               && !handlerType.IsGenericTypeDefinition
               && _selected.Contains(handlerType.GetGenericTypeDefinition()));

    /// <summary>
    /// Records that this container registered <paramref name="participantType"/>. Repeated
    /// and overlapping calls are safe — selection is a union.
    /// </summary>
    public void Select(Type participantType)
    {
        ArgumentNullException.ThrowIfNull(participantType);

        lock (_selected)
        {
            _selected.Add(participantType);
        }
    }

    /// <summary>
    /// Records a whole registration batch; see <see cref="Select(Type)"/>.
    /// </summary>
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
    /// Everything registration has named so far — messages and participants alike, in no
    /// particular order. The raw selection, before it is matched against the table; see
    /// <see cref="SelectedParticipants"/> for the part that names a pipeline participant.
    /// </summary>
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
    /// The participant types this container both registered and can actually run: its
    /// selection intersected with the types the compiled table (plus any composition
    /// handed over directly) names as participants. This is what registration derives the
    /// container's handler service registrations from — selection alone would also carry
    /// the message types registration names, which are constructs to dispatch, not
    /// services to resolve.
    /// </summary>
    /// <remarks>
    /// Setup-time only, and deliberately so: it reads the whole table once, while
    /// registration is still open and before any projection is cached.
    /// <para>
    /// Every type returned came through <see cref="FrozenParticipant.HandlerType"/>, so its
    /// public constructors are preserved under trimming — the sequence itself cannot carry
    /// the annotation, which is why the caller registering these as services suppresses the
    /// dataflow warning rather than re-stating the requirement.
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

            // Local entries state the pipeline rather than choose from one, so their
            // participants are taken whole — the same rule the projection applies.
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
    /// The composition serving a runtime message type in this container: the process-wide
    /// table entry (found exactly, or through the ancestor ladder) narrowed to the rows
    /// this container registered. <c>null</c> when no entry serves the type at all — the
    /// caller's no-handler guard.
    /// </summary>
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
    /// Narrows a table entry to the selected rows, returning the entry itself when every
    /// row survives — the overwhelmingly common case (one container registering the whole
    /// closure), which then costs no allocation and keeps composition identity stable.
    /// </summary>
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
    /// The selected rows of one segment, order preserved; the input array itself when
    /// nothing was dropped.
    /// </summary>
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
