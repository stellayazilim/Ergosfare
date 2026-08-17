namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// What is passed for one parameter of a plugin method.
/// </summary>
/// <remarks>
/// Decided from the parameter's type while the plugin is discovered, so writing the call is
/// a straight substitution.
/// </remarks>
internal enum PluginParameterKind : byte
{
    /// <summary>The dispatched message — the method's own type parameter.</summary>
    Message,

    /// <summary>The execution context.</summary>
    Context,

    /// <summary>Anything else, resolved from the dispatching provider at the call site.</summary>
    Service,
}
