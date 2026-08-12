using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Which plan boundary a plugin call is emitted at. Mirrors
///     <c>Stella.Ergosfare.Plugins.Abstractions.Stage</c> by numeric value — the generator
///     cannot reference that assembly, so it reads the attribute's constructor argument and
///     maps it here.
/// </summary>
/// <remarks>
///     The numeric values are a compatibility surface: a plugin compiled against one version
///     of the abstractions carries the number, not the name. They are never renumbered.
/// </remarks>
internal enum PluginStage
{
    PipelineStart = 0,
    PreMainHandler = 1,
    PostMainHandler = 2,
    AfterPost = 3,
    OnException = 4,
    OnFinal = 5,
}

/// <summary>
///     Which pipeline shape a plugin method was declared for. The two are separate
///     declarations because their signatures genuinely differ: a resultless pipeline has no
///     result to observe, and folding both into one attribute would force every author to
///     carry a <c>TResult</c> that is always <c>Unit</c>.
/// </summary>
internal enum PluginPipelineShape : byte
{
    /// <summary><c>[PipelineInvokable]</c> — pipelines that produce a result.</summary>
    Result,

    /// <summary><c>[VoidPipelineInvokable]</c> — resultless commands and event broadcasts.</summary>
    Void,
}

/// <summary>
///     What the generator passes for one parameter of a plugin method, decided from the
///     parameter's type at discovery so emission is a straight substitution.
/// </summary>
internal enum PluginParameterKind : byte
{
    /// <summary>The dispatched message — the method's first type parameter.</summary>
    Message,

    /// <summary>The pipeline result — the method's second type parameter.</summary>
    Result,

    /// <summary>The execution context.</summary>
    Context,

    /// <summary>Anything else: resolved from the dispatching provider at the call site.</summary>
    Service,
}

/// <summary>
///     Mirrors <c>Stella.Ergosfare.Plugins.Abstractions.Module</c> by numeric value; see
///     <see cref="PluginStage"/> for why the values are pinned.
/// </summary>
[Flags]
internal enum PluginModule
{
    None = 0,
    Command = 1 << 0,
    Query = 1 << 1,
    Event = 1 << 2,
    All = Command | Query | Event,
}

/// <summary>
///     One <c>[PipelineInvokable]</c> method discovered on a plugin service: everything
///     emission needs to write the call, plus the filters deciding which plans it is written
///     into.
/// </summary>
/// <remarks>
///     <para>
///         The method is an observer — it returns <c>void</c> or <c>ValueTask</c> and does
///         not rewrite the message or the result. <see cref="IsAsync"/> selects between a
///         plain call and an awaited one; a <c>void</c> method never enters an async state
///         machine, which is the whole point of the cheap shape.
///     </para>
///     <para>
///         <see cref="Arity"/> is the method's own generic arity. The generator closes it
///         over the plan's concrete message (and result) types at each emission site, so a
///         value-typed message or result is never boxed on the way in.
///     </para>
/// </remarks>
internal readonly record struct PluginInvocationModel(
    string ServiceTypeExpression,
    string ServiceDisplayName,
    string MethodName,
    PluginStage Stage,
    PluginPipelineShape Shape,
    bool IsAsync,
    bool IsStatic,
    int Arity,
    PluginModule Modules,
    ImmutableArray<string> Keys,
    ImmutableArray<PluginParameterBinding> Parameters,
    PluginConstraintModel Constraints,
    LocationInfo? Location)
{
    /// <summary>
    ///     Whether the filter says nothing about discovery keys, which selects the default
    ///     key alone — a keyed construct is opted out of default discovery by its author and
    ///     a silent plugin should not opt it back in.
    /// </summary>
    public bool SelectsDefaultKeyOnly => Keys.IsDefaultOrEmpty;

    public bool Equals(PluginInvocationModel other)
        => ServiceTypeExpression == other.ServiceTypeExpression
           && MethodName == other.MethodName
           && Stage == other.Stage
           && Shape == other.Shape
           && IsAsync == other.IsAsync
           && IsStatic == other.IsStatic
           && Arity == other.Arity
           && Modules == other.Modules
           && Constraints == other.Constraints
           && Keys.SequenceEqualOrBothEmpty(other.Keys)
           && Parameters.SequenceEqualOrBothEmpty(other.Parameters);

    public override int GetHashCode()
    {
        var hash = ServiceTypeExpression.GetHashCode();
        hash = (hash * 397) ^ MethodName.GetHashCode();
        hash = (hash * 397) ^ (int)Stage;
        hash = (hash * 397) ^ (int)Shape;
        hash = (hash * 397) ^ Arity;
        hash = (hash * 397) ^ (int)Modules;
        hash = (hash * 397) ^ Parameters.Length;
        hash = (hash * 397) ^ Constraints.GetHashCode();
        hash = (hash * 397) ^ (Keys.IsDefaultOrEmpty ? 0 : Keys.Length);
        return hash;
    }
}

/// <summary>
///     The generic constraints on a plugin method's message type parameter — the shape
///     filter of the design: the generator closes the method over each plan's concrete
///     message, so a plan whose message does not satisfy them must not get the call. Both
///     because it would be wrong and because it would not compile.
/// </summary>
/// <param name="MessageTypes">
///     The normalized type expressions the message must be assignable to; matched against
///     the message's own base-and-interface chain.
/// </param>
/// <param name="RequiresReferenceType"><c>where TMessage : class</c>.</param>
/// <param name="RequiresValueType"><c>where TMessage : struct</c>.</param>
/// <param name="IsUnmodelable">
///     A constraint the string model cannot decide — a <c>new()</c> or <c>unmanaged</c>
///     constraint, one naming another type parameter, or any constraint at all on the
///     result parameter. Such a method is left out of every plan: emitting it risks a
///     broken consumer build, which is worse than the miss.
/// </param>
internal readonly record struct PluginConstraintModel(
    ImmutableArray<string> MessageTypes,
    bool RequiresReferenceType,
    bool RequiresValueType,
    bool IsUnmodelable)
{
    public static readonly PluginConstraintModel None =
        new(ImmutableArray<string>.Empty, false, false, false);

    public bool Equals(PluginConstraintModel other)
        => RequiresReferenceType == other.RequiresReferenceType
           && RequiresValueType == other.RequiresValueType
           && IsUnmodelable == other.IsUnmodelable
           && MessageTypes.SequenceEqualOrBothEmpty(other.MessageTypes);

    public override int GetHashCode()
    {
        var hash = RequiresReferenceType ? 1 : 0;
        hash = (hash * 397) ^ (RequiresValueType ? 1 : 0);
        hash = (hash * 397) ^ (IsUnmodelable ? 1 : 0);
        hash = (hash * 397) ^ (MessageTypes.IsDefaultOrEmpty ? 0 : MessageTypes.Length);
        return hash;
    }
}

/// <summary>One parameter of a plugin method, resolved to what emission substitutes for it.</summary>
/// <param name="Kind">Where the argument comes from.</param>
/// <param name="TypeExpression">
///     For <see cref="PluginParameterKind.Service"/>, the fully qualified type to resolve
///     from the dispatching provider; <c>null</c> for every other kind.
/// </param>
internal readonly record struct PluginParameterBinding(PluginParameterKind Kind, string? TypeExpression);

internal static class PluginModelExtensions
{
    /// <summary>
    ///     Sequence equality that treats a default array as empty — discovery leaves both
    ///     shapes behind and the incremental pipeline caches on value equality.
    /// </summary>
    internal static bool SequenceEqualOrBothEmpty<T>(this ImmutableArray<T> left, ImmutableArray<T> right)
    {
        if (left.IsDefaultOrEmpty)
        {
            return right.IsDefaultOrEmpty;
        }

        if (right.IsDefaultOrEmpty || left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }
}
