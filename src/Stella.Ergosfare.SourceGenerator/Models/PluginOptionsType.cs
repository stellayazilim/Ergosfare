using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// A plugin's options type, in the forms emission and diagnostics each need.
/// </summary>
/// <param name="TypeExpression">The fully qualified name to write into generated code.</param>
/// <param name="DisplayName">The name to show in a diagnostic message.</param>
/// <param name="Symbol">The symbol itself, for locating and comparing.</param>
internal readonly record struct PluginOptionsType(string TypeExpression, string DisplayName, ISymbol Symbol);
