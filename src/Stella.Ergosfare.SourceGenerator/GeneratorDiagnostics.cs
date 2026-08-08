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

    /// <summary>
    ///     A handler with several public constructors keeps the container's greedy,
    ///     content-dependent constructor selection in play, so the generated plans cannot
    ///     prove direct construction identical to container activation and leave the
    ///     handler on the container path. Informational: everything still works, only the
    ///     construction fast path is lost.
    /// </summary>
    public static readonly DiagnosticDescriptor MultiplePublicConstructors = new(
        id: "ERGOSG003",
        title: "Multiple public constructors keep the handler on the container path",
        messageFormat:
            "Handler '{0}' has more than one public constructor, so generated plans cannot prove which one the " +
            "container would pick and skip its direct-construction fast path. Collapse to a single public " +
            "constructor to enable it.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    ///     <c>[FromServices]</c> is an ASP.NET Core action-parameter attribute; on a
    ///     constructor parameter it does nothing — constructor injection resolves services
    ///     regardless. Informational so the stray attribute does not suggest behavior that
    ///     is not there.
    /// </summary>
    public static readonly DiagnosticDescriptor FromServicesOnConstructor = new(
        id: "ERGOSG004",
        title: "[FromServices] has no effect on constructor parameters",
        messageFormat:
            "Type '{0}' carries [FromServices] on a constructor parameter, where it has no effect — constructor " +
            "injection resolves services regardless. Remove the attribute; for keyed services use [FromKeyedServices].",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}
