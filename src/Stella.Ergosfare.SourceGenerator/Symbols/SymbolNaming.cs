using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
///     How a symbol is spelled in the generated file, and whether it can be spelled there
///     at all. Every type the emitted code names goes through here, so one answer serves
///     the registration surface, the plan bodies and the frozen composition table alike.
/// </summary>
internal static class SymbolNaming
{
    /// <summary>
    ///     The fully qualified <c>typeof</c> argument for a type exactly as declared —
    ///     constructed generics included.
    /// </summary>
    internal static string VerbatimTypeExpression(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    ///     The fully qualified <c>typeof</c> argument for a type, with generic types
    ///     normalized to their unbound definitions — mirroring the interceptor descriptor
    ///     builders' <c>GetGenericTypeDefinition()</c> normalization.
    /// </summary>
    internal static string NormalizedTypeExpression(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true } named
            ? BuildTypeofExpression(named.OriginalDefinition)
            : VerbatimTypeExpression(type);

    /// <summary>
    ///     Whether the generated file can name the type at all: file-local, private and
    ///     protected types have no spelling a separate file can use.
    /// </summary>
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
    ///     Builds the fully qualified <c>typeof</c> argument for a type, walking the
    ///     containing-type chain so nested types render correctly. Generic definitions use
    ///     the unbound form (<c>Foo&lt;,&gt;</c>) — mixing bound and unbound levels is not
    ///     legal C#, and every discovered type is a definition, never a constructed generic.
    /// </summary>
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
    ///     Checks that a symbol lives exactly in the given dotted namespace.
    /// </summary>
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

        return ns is { IsGlobalNamespace: true };
    }

    /// <summary>
    ///     Whether every name in the type's containing chain is a legal C# identifier —
    ///     compiler-generated and other unspellable names cannot appear in emitted source.
    /// </summary>
    /// <summary>
    ///     Whether the type's full containing chain uses names spellable in C# source.
    ///     File-local types survive into metadata as internal types with compiler-mangled
    ///     names (<c>&lt;File&gt;F...__Type</c>) that a <c>typeof</c> cannot express.
    /// </summary>
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
    ///     The CLR metadata name of a type definition (<c>Ns.Type`1</c>, nested via
    ///     <c>+</c>) — the manifest attribute's payload, resolvable back to a symbol by
    ///     <c>GetTypeByMetadataName</c> in an aggregating compilation.
    /// </summary>
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
