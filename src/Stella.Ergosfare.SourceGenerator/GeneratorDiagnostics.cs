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

    /// <summary>
    ///     A marker type in a scanned referenced assembly cannot be named by generated
    ///     code (internal without <c>InternalsVisibleTo</c>, protected/private nested, or
    ///     compiler-mangled), so source-generated registration skipped it. The runtime
    ///     scanning path (<c>RegisterFromAssembly</c>) sees such types through reflection,
    ///     which makes the divergence worth a warning.
    /// </summary>
    public static readonly DiagnosticDescriptor InvisibleReferencedRegistrableType = new(
        id: "ERGOSG002",
        title: "Registrable type in a referenced assembly is not visible to generated registration code",
        messageFormat:
            "Type '{0}' in referenced assembly '{1}' implements an Ergosfare marker interface but is not visible " +
            "to this compilation, so source-generated registration skipped it. Grant this compilation access with " +
            "[InternalsVisibleTo], register the type manually, or register that assembly at runtime with RegisterFromAssembly.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
