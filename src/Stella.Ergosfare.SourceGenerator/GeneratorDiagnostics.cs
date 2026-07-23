using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     Diagnostics reported by the Ergosfare source generator (ERGOSG prefix).
/// </summary>
internal static class GeneratorDiagnostics
{
    /// <summary>
    ///     A type carries an Ergosfare marker interface but generated code cannot reference
    ///     it, so it is silently invisible to source-generated registration. The runtime
    ///     scanning path (<c>RegisterFromAssembly</c>) would have picked it up, which makes
    ///     the mismatch worth a warning rather than silent divergence.
    /// </summary>
    public static readonly DiagnosticDescriptor InaccessibleRegistrableType = new(
        id: "ERGOSG001",
        title: "Registrable type is not accessible from generated registration code",
        messageFormat:
            "Type '{0}' implements an Ergosfare marker interface but is private, protected, or file-local, " +
            "so source-generated registration cannot reference it and skipped it. " +
            "Make the type at least internal, or register it manually.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
