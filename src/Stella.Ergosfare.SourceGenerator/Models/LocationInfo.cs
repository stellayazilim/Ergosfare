using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// A source location captured as plain values, so it can be compared by value.
/// </summary>
/// <param name="FilePath">The file the location is in.</param>
/// <param name="TextSpan">The span within the file.</param>
/// <param name="LineSpan">The same span as line and column positions.</param>
/// <remarks>
/// The incremental pipeline's models must not hold a Roslyn location: it keeps the whole
/// syntax tree alive and compares by tree identity, so caching would never hit. Diagnostics
/// carry this instead and rebuild a location when they are reported.
/// </remarks>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    /// <summary>
    /// Rebuilds a Roslyn location for reporting a diagnostic.
    /// </summary>
    /// <returns>The location.</returns>
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    /// <summary>
    /// Captures where a symbol is declared.
    /// </summary>
    /// <param name="symbol">The symbol to locate.</param>
    /// <returns>
    /// Its first declaration in source, or <c>null</c> when it has none — a symbol from a
    /// referenced assembly, for instance.
    /// </returns>
    public static LocationInfo? From(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            if (location.SourceTree is not null)
            {
                return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
            }
        }

        return null;
    }

    /// <summary>
    /// Captures where a syntax node sits.
    /// </summary>
    /// <param name="node">The node to locate.</param>
    /// <returns>Its location, or <c>null</c> when it has none.</returns>
    public static LocationInfo? From(SyntaxNode node)
    {
        var location = node.GetLocation();

        return location.SourceTree is null
            ? null
            : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }
}
