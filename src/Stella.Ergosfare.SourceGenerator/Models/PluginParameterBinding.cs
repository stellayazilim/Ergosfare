using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>One parameter of a plugin method, resolved to what emission substitutes for it.</summary>
/// <param name="Kind">Where the argument comes from.</param>
/// <param name="TypeExpression">
///     For <see cref="PluginParameterKind.Service"/>, the fully qualified type to resolve
///     from the dispatching provider; <c>null</c> for every other kind.
/// </param>
internal readonly record struct PluginParameterBinding(PluginParameterKind Kind, string? TypeExpression);
