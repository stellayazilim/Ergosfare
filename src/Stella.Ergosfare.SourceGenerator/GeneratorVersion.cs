
namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// The generator's own version.
/// </summary>
/// <remarks>
/// Written into the header of every generated file and into the dispatch manifest, so a
/// build's output says which generator produced it.
/// </remarks>
internal static class GeneratorVersion
{
    /// <summary>
    /// The version, read from this assembly.
    /// </summary>
    internal static readonly string Value =
        typeof(ErgosfareRegistrationGenerator).Assembly.GetName().Version?.ToString() ?? "1.0.0";
}
