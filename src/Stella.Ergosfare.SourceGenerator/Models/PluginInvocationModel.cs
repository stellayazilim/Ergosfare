using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     One <c>[PipelineInvokable]</c> method discovered on a plugin service: everything
///     emission needs to write the call, plus the filters deciding which plans it is written
///     into.
/// </summary>
/// <remarks>
///     <para>
///         The method is an observer — it returns <c>void</c> or <c>ValueTask</c> and does
///         not rewrite the message or produce a result. <see cref="IsAsync"/> selects between
///         a plain call and an awaited one; a <c>void</c> method never enters an async state
///         machine, which is the whole point of the cheap shape.
///     </para>
///     <para>
///         The method is generic over the message alone — no hook carries a result, so there
///         is one signature shape and one arity. The generator closes it over the plan's
///         concrete message type at each emission site, so a value-typed message is never
///         boxed on the way in; a method of any other arity is not a hook this emission can
///         write and is dropped at discovery.
///     </para>
/// </remarks>
internal readonly record struct PluginInvocationModel(
    string ServiceTypeExpression,
    string ServiceDisplayName,
    string MethodName,
    PluginHook Hook,
    bool IsAsync,
    bool IsStatic,
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
           && Hook == other.Hook
           && IsAsync == other.IsAsync
           && IsStatic == other.IsStatic
           && Modules == other.Modules
           && Constraints == other.Constraints
           && Keys.SequenceEqualOrBothEmpty(other.Keys)
           && Parameters.SequenceEqualOrBothEmpty(other.Parameters);

    public override int GetHashCode()
    {
        var hash = ServiceTypeExpression.GetHashCode();
        hash = (hash * 397) ^ MethodName.GetHashCode();
        hash = (hash * 397) ^ (int)Hook;
        hash = (hash * 397) ^ (int)Modules;
        hash = (hash * 397) ^ Parameters.Length;
        hash = (hash * 397) ^ Constraints.GetHashCode();
        hash = (hash * 397) ^ (Keys.IsDefaultOrEmpty ? 0 : Keys.Length);
        return hash;
    }
}
