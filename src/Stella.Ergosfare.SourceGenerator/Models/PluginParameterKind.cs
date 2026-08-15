namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     What the generator passes for one parameter of a plugin method, decided from the
///     parameter's type at discovery so emission is a straight substitution.
/// </summary>
internal enum PluginParameterKind : byte
{
    /// <summary>The dispatched message — the method's type parameter.</summary>
    Message,

    /// <summary>The execution context.</summary>
    Context,

    /// <summary>Anything else: resolved from the dispatching provider at the call site.</summary>
    Service,
}
