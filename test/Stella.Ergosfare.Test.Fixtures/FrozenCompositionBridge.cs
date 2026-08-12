using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions;

namespace Stella.Ergosfare.Test.Fixtures;

/// <summary>
/// Builds the <see cref="FrozenComposition"/> the dispatch path reads out of a message
/// type and the participant types serving it — the test-side counterpart of what the
/// source generator bakes, for the constructs the generator does not model (handlers
/// declared purely over the core contracts, with no module marker).
/// </summary>
/// <remarks>
/// Classification mirrors the rules the compilation applies: a participant whose contract
/// closes over the message type exactly is a direct row, one closing over a supertype is a
/// covariant (indirect) row, rows come out in the emitter's order (weight descending, then
/// ordinal CLR <c>FullName</c>), and the message's <c>[ExcludeFromPipeline]</c> is resolved
/// here rather than at dispatch. This is a fixture, so it must produce the same table a
/// compilation would, not merely one that happens to work.
/// </remarks>
public static class FrozenCompositionBridge
{
    /// <summary>
    /// The segment a participant contract contributes to. The result type each contract
    /// also carries is metadata the composition does not model — a row is a handler type
    /// and its groups.
    /// </summary>
    private enum Segment
    {
        MainHandler,
        PreInterceptor,
        PostInterceptor,
        ExceptionInterceptor,
        FinalInterceptor,
    }

    /// <summary>
    /// Every participant contract, paired with the segment it feeds. The message type is
    /// always the contract's first type argument.
    /// </summary>
    private static readonly (Type Contract, Segment Segment)[] Contracts =
    [
        (typeof(IHandler<,>), Segment.MainHandler),
        (typeof(IAsyncHandler<>), Segment.MainHandler),
        (typeof(IAsyncHandler<,>), Segment.MainHandler),
        (typeof(IStreamHandler<,>), Segment.MainHandler),
        (typeof(IPreInterceptor<>), Segment.PreInterceptor),
        (typeof(IAsyncPreInterceptor<>), Segment.PreInterceptor),
        (typeof(IPostInterceptor<,>), Segment.PostInterceptor),
        (typeof(IAsyncPostInterceptor<,>), Segment.PostInterceptor),
        (typeof(IAsyncPostInterceptor<>), Segment.PostInterceptor),
        (typeof(IExceptionInterceptor<,>), Segment.ExceptionInterceptor),
        (typeof(IAsyncExceptionInterceptor<,>), Segment.ExceptionInterceptor),
        (typeof(IAsyncExceptionInterceptor<>), Segment.ExceptionInterceptor),
        (typeof(IFinalInterceptor<,>), Segment.FinalInterceptor),
        (typeof(IAsyncFinalInterceptor<,>), Segment.FinalInterceptor),
        (typeof(IAsyncFinalInterceptor<>), Segment.FinalInterceptor),
    ];

    /// <summary>
    /// The message's whole composition, ready to hand to a catalog. Participant types that
    /// serve some other message contribute nothing; the message type itself may be listed
    /// among them and is simply ignored.
    /// </summary>
    public static FrozenComposition FromTypes(Type messageType, IEnumerable<Type> participantTypes)
    {
        var rows = new Dictionary<(Segment Segment, bool Indirect), List<Type>>();

        foreach (var participantType in participantTypes)
        {
            foreach (var (contract, segment) in Contracts)
            {
                foreach (var closed in participantType.GetInterfacesEqualTo(contract))
                {
                    var declared = closed.GetGenericArguments()[0];

                    bool indirect;

                    if (Normalize(declared) == Normalize(messageType))
                    {
                        indirect = false;
                    }
                    else if (messageType.IsAssignableTo(declared))
                    {
                        indirect = true;
                    }
                    else
                    {
                        continue;
                    }

                    var key = (segment, indirect);

                    if (!rows.TryGetValue(key, out var bucket))
                    {
                        rows[key] = bucket = [];
                    }

                    if (!bucket.Contains(participantType))
                    {
                        bucket.Add(participantType);
                    }
                }
            }
        }

        var exclusion = (ExcludeFromPipelineAttribute?)Attribute.GetCustomAttribute(
            messageType, typeof(ExcludeFromPipelineAttribute));

        return new FrozenComposition(
            messageType,
            Direct(Segment.MainHandler),
            Indirect(Segment.MainHandler),
            Direct(Segment.PreInterceptor),
            Indirect(Segment.PreInterceptor),
            Direct(Segment.PostInterceptor),
            Indirect(Segment.PostInterceptor),
            Direct(Segment.ExceptionInterceptor),
            Indirect(Segment.ExceptionInterceptor),
            Direct(Segment.FinalInterceptor),
            Indirect(Segment.FinalInterceptor));

        FrozenParticipant[] Direct(Segment segment)
            => Order(rows.TryGetValue((segment, false), out var bucket) ? bucket : []);

        // A covariantly matched interceptor stage, with the message's
        // [ExcludeFromPipeline] resolved: the blanket form empties it, the group-scoped
        // form drops the interceptors carrying an excluded group. Main handlers are never
        // touched — exclusion is about interceptors reaching in, not about who handles.
        FrozenParticipant[] Indirect(Segment segment)
        {
            var bucket = rows.TryGetValue((segment, true), out var found) ? found : [];

            if (segment == Segment.MainHandler || exclusion is null)
            {
                return Order(bucket);
            }

            if (exclusion.Groups.Length == 0)
            {
                return [];
            }

            var kept = new List<Type>(bucket.Count);

            foreach (var participantType in bucket)
            {
                var groups = participantType.GetGroupsFromAttribute();
                var dropped = false;

                foreach (var group in groups)
                {
                    foreach (var excludedGroup in exclusion.Groups)
                    {
                        dropped |= string.Equals(group, excludedGroup, StringComparison.Ordinal);
                    }
                }

                if (!dropped)
                {
                    kept.Add(participantType);
                }
            }

            return Order(kept);
        }
    }

    /// <summary>
    /// Generic messages are keyed by their definition, exactly as the compiled table keys
    /// them, so a contract closing over <c>Message&lt;T&gt;</c> matches <c>Message&lt;int&gt;</c>.
    /// </summary>
    private static Type Normalize(Type type)
        => type.IsGenericType ? type.GetGenericTypeDefinition() : type;

    private static FrozenParticipant[] Order(List<Type> participantTypes)
    {
        if (participantTypes.Count == 0)
        {
            return [];
        }

        var ordered = new List<Type>(participantTypes);

        ordered.Sort(static (x, y) =>
        {
            var byWeight = y.GetWeightFromAttribute().CompareTo(x.GetWeightFromAttribute());

            return byWeight != 0
                ? byWeight
                : string.CompareOrdinal(x.FullName, y.FullName);
        });

        var rows = new FrozenParticipant[ordered.Count];

        for (var i = 0; i < ordered.Count; i++)
        {
            rows[i] = new FrozenParticipant(ordered[i], [.. ordered[i].GetGroupsFromAttribute()]);
        }

        return rows;
    }
}
