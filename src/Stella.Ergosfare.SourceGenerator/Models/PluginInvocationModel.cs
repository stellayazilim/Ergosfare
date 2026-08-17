using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One <c>[PipelineInvokable]</c> method found on a plugin service: what writing the call
/// needs, and the filters deciding which pipelines it is written into.
/// </summary>
/// <param name="ServiceTypeExpression">The declaring service's fully qualified type.</param>
/// <param name="ServiceDisplayName">The service as a diagnostic would name it.</param>
/// <param name="MethodName">The method to call.</param>
/// <param name="Hook">Where in the pipeline to call it.</param>
/// <param name="IsAsync">Whether the call must be awaited.</param>
/// <param name="IsStatic">Whether the method is called on the type rather than an instance.</param>
/// <param name="Modules">The message families the method applies to.</param>
/// <param name="Keys">The discovery keys the method applies to.</param>
/// <param name="Parameters">What to pass for each parameter.</param>
/// <param name="Constraints">The constraints a message must satisfy for the call to be written.</param>
/// <param name="Location">Where the method is declared, for diagnostics.</param>
/// <remarks>
/// <para>
/// The method observes: it returns nothing or a task, and neither rewrites the message nor
/// produces a result. A method returning nothing is called plainly and never enters an async
/// state machine, which is what makes that shape cheap.
/// </para>
/// <para>
/// It is generic over the message alone, since no hook carries a result — one signature,
/// one arity. It is closed over each pipeline's own message type where the call is written,
/// so a value-typed message is never boxed on the way in; a method of any other arity is not
/// something this can write and is dropped as it is discovered.
/// </para>
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
    /// Whether the method says nothing about discovery keys, which selects the default key
    /// alone.
    /// </summary>
    /// <remarks>
    /// A keyed construct was kept out of default discovery by its author, and a plugin
    /// saying nothing should not put it back in.
    /// </remarks>
    public bool SelectsDefaultKeyOnly => Keys.IsDefaultOrEmpty;

    /// <summary>
    /// Compares everything that changes where and how the call is written.
    /// </summary>
    /// <param name="other">The model to compare against.</param>
    /// <returns><c>true</c> when both would produce the same calls.</returns>
    /// <remarks>
    /// The display name and location are left out, since they only affect diagnostic text.
    /// Written by hand because the generated comparison would compare the arrays by
    /// reference.
    /// </remarks>
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

    /// <summary>
    /// Returns a hash over the service, the method, the hook and the filters.
    /// </summary>
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
