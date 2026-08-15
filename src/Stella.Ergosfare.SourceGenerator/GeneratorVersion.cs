using System.Reflection;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     The generator's own version, stamped into the emitted file's header and its dispatch
///     manifest so a consumer's build output says which generator produced it.
/// </summary>
internal static class GeneratorVersion
{
    internal static readonly string Value =
        typeof(ErgosfareRegistrationGenerator).Assembly.GetName().Version?.ToString() ?? "1.0.0";
}
