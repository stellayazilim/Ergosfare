using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
/// How a symbol is written into generated code, and whether it can be written there at all.
/// </summary>
/// <remarks>
/// Every type the generated code names comes through here, so one answer serves the
/// registration surface, the plan bodies and the composition table alike.
/// </remarks>
internal static class SymbolNaming
{
    /// <summary>
    /// Returns a type's fully qualified name exactly as declared, keeping the type
    /// arguments of a constructed generic.
    /// </summary>
    /// <param name="type">The type to write.</param>
    /// <returns>The fully qualified name.</returns>
    internal static string VerbatimTypeExpression(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    /// Returns a type's fully qualified name with a generic reduced to its unbound
    /// definition.
    /// </summary>
    /// <param name="type">The type to write.</param>
    /// <returns>The fully qualified name, generic arguments dropped.</returns>
    /// <remarks>
    /// This is how an interceptor's registration names its message type, so that one
    /// registration covers every closing of a generic message.
    /// </remarks>
    internal static string NormalizedTypeExpression(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true } named
            ? BuildTypeofExpression(named.OriginalDefinition)
            : VerbatimTypeExpression(type);

    /// <summary>
    /// Reports whether generated code can name a type at all.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <returns><c>true</c> when a separate file could refer to it.</returns>
    /// <remarks>
    /// A file-local, private or protected type — at any level of its containing chain — has
    /// no name another file can use.
    /// </remarks>
    internal static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }

            if (current.DeclaredAccessibility is Accessibility.Private
                or Accessibility.Protected
                or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds a type's fully qualified name, walking its containing types so a nested type
    /// comes out right.
    /// </summary>
    /// <param name="symbol">The type to write.</param>
    /// <returns>The fully qualified name.</returns>
    /// <remarks>
    /// A generic is written in its unbound form. Mixing bound and unbound levels is not
    /// legal C#, and every type reaching here is a definition rather than a constructed
    /// generic.
    /// </remarks>
    internal static string BuildTypeofExpression(INamedTypeSymbol symbol)
    {
        var parts = new Stack<string>();

        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            parts.Push(current.Arity == 0
                ? current.Name
                : current.Name + "<" + new string(',', current.Arity - 1) + ">");
        }

        var sb = new StringBuilder("global::");

        if (symbol.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            sb.Append(ns.ToDisplayString()).Append('.');
        }

        // The stack was filled innermost first, so popping it writes the containing types
        // outermost first.
        var first = true;
        foreach (var part in parts)
        {
            if (!first)
            {
                sb.Append('.');
            }

            sb.Append(part);
            first = false;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Reports whether a type sits in exactly the given namespace.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <param name="expectedNamespace">The dotted namespace to match.</param>
    /// <returns><c>true</c> when the type's namespace is exactly that one.</returns>
    /// <remarks>
    /// Compared segment by segment from the innermost outwards, so nothing is allocated to
    /// answer it.
    /// </remarks>
    internal static bool IsInNamespace(INamedTypeSymbol symbol, string expectedNamespace)
    {
        var ns = symbol.ContainingNamespace;

        for (var end = expectedNamespace.Length; end > 0;)
        {
            if (ns is null || ns.IsGlobalNamespace)
            {
                return false;
            }

            var start = expectedNamespace.LastIndexOf('.', end - 1) + 1;

            if (ns.Name.Length != end - start
                || string.CompareOrdinal(expectedNamespace, start, ns.Name, 0, ns.Name.Length) != 0)
            {
                return false;
            }

            ns = ns.ContainingNamespace;
            end = start - 1;
        }

        // Every segment matched, so this must now be the global namespace — otherwise the
        // type sits deeper than the expected one.
        return ns is { IsGlobalNamespace: true };
    }

    /// <summary>
    /// Reports whether every name in a type's containing chain can be written in C# source.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <returns><c>true</c> when the whole chain is spellable.</returns>
    /// <remarks>
    /// A file-local type survives into metadata as an internal type with a compiler-mangled
    /// name that no <c>typeof</c> can express, and the same goes for other
    /// compiler-generated names.
    /// </remarks>
    internal static bool HasSpellableName(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (!SyntaxFacts.IsValidIdentifier(current.Name))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds a type's CLR metadata name — the form a manifest records.
    /// </summary>
    /// <param name="symbol">The type to name.</param>
    /// <returns>
    /// The metadata name, with arity marked and nested types joined by <c>+</c>, which
    /// another compilation can resolve back to a symbol.
    /// </returns>
    internal static string BuildMetadataName(INamedTypeSymbol symbol)
    {
        var parts = new Stack<string>();

        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            parts.Push(current.MetadataName);
        }

        var sb = new StringBuilder();

        if (symbol.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            sb.Append(ns.ToDisplayString()).Append('.');
        }

        var first = true;

        foreach (var part in parts)
        {
            if (!first)
            {
                sb.Append('+');
            }

            sb.Append(part);
            first = false;
        }

        return sb.ToString();
    }
}
