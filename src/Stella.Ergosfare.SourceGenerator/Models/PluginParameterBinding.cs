

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One parameter of a plugin method, resolved to what is written in its place at the call
/// site.
/// </summary>
/// <param name="Kind">Where the argument comes from.</param>
/// <param name="TypeExpression">
/// The type to resolve from the dispatching provider, for
/// <see cref="PluginParameterKind.Service"/>; <c>null</c> for every other kind.
/// </param>
internal readonly record struct PluginParameterBinding(PluginParameterKind Kind, string? TypeExpression);
