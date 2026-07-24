using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Value-equatable snapshot of a source location. Incremental pipeline models must not
///     hold <see cref="Location"/> (it pins the syntax tree and compares by tree identity),
///     so diagnostics carry this snapshot and rehydrate a <see cref="Location"/> on report.
/// </summary>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    /// <summary>
    ///     Rehydrates a Roslyn <see cref="Location"/> for diagnostic reporting.
    /// </summary>
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    /// <summary>
    ///     Captures the location of a symbol's first source declaration, or <c>null</c>
    ///     when the symbol has no source location.
    /// </summary>
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
}
