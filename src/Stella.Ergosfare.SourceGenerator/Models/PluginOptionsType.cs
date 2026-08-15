using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>The plugin's settings type as emission and diagnostics need to name it.</summary>
internal readonly record struct PluginOptionsType(string TypeExpression, string DisplayName, ISymbol Symbol);
