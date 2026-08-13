using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     Diagnostics reported by the Ergosfare source generator (ERGOSG prefix).
/// </summary>
internal static class GeneratorDiagnostics
{
    /// <summary>
    ///     A type carries an Ergosfare marker interface but generated code cannot reference
    ///     it, so it is silently invisible to source-generated registration — worth a
    ///     warning rather than silent divergence from what the declaration promises.
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
    ///     compiler-mangled), so source-generated registration skipped it — worth a
    ///     warning rather than silent divergence from what the declaration promises.
    /// </summary>
    public static readonly DiagnosticDescriptor InvisibleReferencedRegistrableType = new(
        id: "ERGOSG002",
        title: "Registrable type in a referenced assembly is not visible to generated registration code",
        messageFormat:
            "Type '{0}' in referenced assembly '{1}' implements an Ergosfare marker interface but is not visible " +
            "to this compilation, so source-generated registration skipped it. Grant this compilation access with " +
            "[InternalsVisibleTo], or register the type manually.",
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

    /// <summary>
    ///     A dispatch site whose static message type — together with every subtype of it
    ///     in the compiled closure — has no covering handler registration: whatever
    ///     instance the expression carries at runtime, the dispatch is guaranteed to fail
    ///     with <c>NoHandlerFoundException</c>. Reported only in composition-root
    ///     compilations, where the closure is complete. An error: the application should
    ///     not compile around a provably dead dispatch.
    /// </summary>
    public static readonly DiagnosticDescriptor DeadDispatch = new(
        id: "ERGOSG005",
        title: "Dispatch can never reach a handler",
        messageFormat:
            "This dispatch of '{0}' can never reach a handler: neither '{0}' nor any of its subtypes in the " +
            "compiled closure has a covering handler registration, so the call is guaranteed to throw " +
            "NoHandlerFoundException at runtime{1}. Register a handler for it or remove the dispatch.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    ///     A dispatch site whose static message type is a concrete, dispatchable message
    ///     with no covering handler, while some subtype of it is covered — an instance of
    ///     exactly the static type fails at runtime even though subtype instances succeed.
    /// </summary>
    public static readonly DiagnosticDescriptor UncoveredStaticDispatch = new(
        id: "ERGOSG006",
        title: "Only subtypes of the dispatched static type are handled",
        messageFormat:
            "The static message type '{0}' of this dispatch has no covering handler; only subtype(s) such as " +
            "'{1}' are handled. An instance of exactly '{0}' throws NoHandlerFoundException at runtime{2}.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    ///     A handler no dispatch site in the compiled closure can reach: no recorded site's
    ///     static type — nor any closure subtype of one — is covered by the handler's
    ///     message registration, so the handler never executes. Judged only in
    ///     composition-root compilations and only when every Ergosfare-referencing assembly
    ///     in the closure carries a dispatch manifest (otherwise sites may be invisible and
    ///     the judgment stays silent).
    /// </summary>
    public static readonly DiagnosticDescriptor UnreachableHandler = new(
        id: "ERGOSG007",
        title: "No dispatch site can reach this handler",
        messageFormat:
            "Handler '{0}' handles '{1}', but no dispatch site in the compiled closure can deliver that " +
            "message{2} — the handler never executes. Suppress ERGOSG007 if the registration is deliberate, or " +
            "set ErgosfareTrimUnusedHandlers=true to exclude such handlers from generated registration.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    ///     Trim report: <c>ErgosfareTrimUnusedHandlers</c> excluded an unreachable handler
    ///     from the generated registration (descriptors, plans and dispatch roots), letting
    ///     the linker drop it. Informational so the exclusion is visible in build logs.
    /// </summary>
    public static readonly DiagnosticDescriptor TrimmedUnreachableHandler = new(
        id: "ERGOSG008",
        title: "Unreachable handler excluded from generated registration",
        messageFormat:
            "Handler '{0}' was excluded from generated registration because no dispatch site in the compiled " +
            "closure reaches it (ErgosfareTrimUnusedHandlers)",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    ///     Strict-mode aid, off by default: the static message type at a dispatch site is
    ///     fully opaque (the bare module marker, <c>IMessage</c>, <c>object</c>, or an
    ///     unconstrained type parameter), so compile-time reachability proofs degrade to
    ///     closure-wide aggregates there. Teams wanting every dispatch statically provable
    ///     raise the severity via .editorconfig
    ///     (<c>dotnet_diagnostic.ERGOSG009.severity = warning</c>).
    /// </summary>
    public static readonly DiagnosticDescriptor OpaqueDispatchSite = new(
        id: "ERGOSG009",
        title: "Dispatch site's static message type is opaque",
        messageFormat:
            "The static message type at this dispatch site is '{0}', which proves nothing about which concrete " +
            "message is dispatched — compile-time dead-dispatch and reachability analysis can only reason about " +
            "it in closure-wide aggregate. Dispatch through a more specific static type to make the site provable.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: false);

    /// <summary>
    ///     Two or more main handlers claim the same message at the same priority level —
    ///     several direct ones, or several covariant ones with no direct handler to win.
    ///     The priority ladder has no tiebreaker within a level, so every dispatch of the
    ///     message fails with <c>MultipleHandlerFoundException</c>; provable at compile
    ///     time, the registration should not compile. Judged only over default-discovery,
    ///     ungrouped handlers in composition-root compilations — keyed or grouped
    ///     registrations are container choices the compiler cannot prove co-registered.
    /// </summary>
    public static readonly DiagnosticDescriptor ContestedMainHandlers = new(
        id: "ERGOSG010",
        title: "Multiple main handlers claim the same message at the same level",
        messageFormat:
            "Message '{0}' is claimed by {1} main handlers at the same {2} level ({3}) — the priority ladder has " +
            "no tiebreaker within a level, so every dispatch of this message throws MultipleHandlerFoundException " +
            "at runtime. Keep exactly one claimant on that level; a direct handler always beats covariant ones.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    ///     A <c>[ResultAdapter]</c> annotation whose adapter type can never bind at
    ///     runtime — it does not implement <c>IResultAdapter&lt;TResult&gt;</c> for any
    ///     result slot the message dispatches, or the runtime binding could not
    ///     instantiate it. An error: the annotation promises value-channel semantics the
    ///     pipeline would silently never deliver (or crash delivering).
    /// </summary>
    public static readonly DiagnosticDescriptor UnbindableResultAdapter = new(
        id: "ERGOSG011",
        title: "Result adapter annotation can never bind",
        messageFormat:
            "The result adapter '{0}' declared on message '{1}' can never bind: {2}. The adapter must implement " +
            "IResultAdapter<TResult> for the message's declared result type and expose a public parameterless " +
            "constructor.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    ///     A message carrying both <c>[ResultAdapter]</c> and <c>[IgnoreResultAdapter]</c>
    ///     (own or inherited, in any combination) declares an adapter and opts out of
    ///     adapters at once. An error: the contradiction has no meaningful resolution —
    ///     remove one of the two.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingResultAdapterAnnotations = new(
        id: "ERGOSG012",
        title: "Conflicting result-adapter annotations",
        messageFormat:
            "Message '{0}' carries both [ResultAdapter] and [IgnoreResultAdapter] (own or inherited) — it declares " +
            "an adapter and opts out of adapters at once. Remove one of the two annotations.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    ///     The application configured a default result adapter, but this result-bearing
    ///     message's declared result type is served by no adapter tier — failures in its
    ///     pipeline cannot travel as values and surface as thrown exceptions instead. An
    ///     error, judged at compile time rather than discovered at dispatch: configuring
    ///     a default declares value-based error handling as the application's contract,
    ///     and a message that silently cannot participate is a design hole. The
    ///     per-message escape is <c>[IgnoreResultAdapter]</c>, which acknowledges the
    ///     throwing pipeline and downgrades the finding to ERGOSG014.
    /// </summary>
    public static readonly DiagnosticDescriptor UnservedByDefaultResultAdapter = new(
        id: "ERGOSG013",
        title: "Result type is not served by the configured default result adapter",
        messageFormat:
            "Message '{0}' declares result type '{1}', which the configured default result adapter '{2}' cannot " +
            "extract failures from — failures in this pipeline surface as thrown exceptions. Return a carrier type " +
            "the adapter serves, declare a [ResultAdapter] for the message, or acknowledge the throwing pipeline " +
            "with [IgnoreResultAdapter].",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    ///     The acknowledged twin of ERGOSG013: the message opted out via
    ///     <c>[IgnoreResultAdapter]</c> while the application's default result adapter
    ///     cannot serve its result type anyway — a deliberate throwing pipeline inside a
    ///     value-channel application, kept visible as a warning. Suppressible through the
    ///     standard channels when the acknowledgment itself is considered enough.
    /// </summary>
    public static readonly DiagnosticDescriptor AcknowledgedThrowingPipeline = new(
        id: "ERGOSG014",
        title: "Opted-out message keeps a throwing pipeline",
        messageFormat:
            "Message '{0}' opted out of result adaptation and its result type '{1}' is not served by the configured " +
            "default result adapter '{2}': failures in this pipeline surface as thrown exceptions caught by the " +
            "pipeline's try/catch — the exception interceptors still run",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    ///     A referenced assembly carries <c>[PipelineInvokable]</c> methods but matches the
    ///     reserved <c>Stella.Ergosfare</c> name prefix, which excludes it from reference
    ///     scanning — so its plugin never reaches a single plan. The exclusion is correct
    ///     for the library's own assemblies; for a plugin package named under the prefix it
    ///     is a silent no-op, which is the worst failure mode a plugin ecosystem can have.
    /// </summary>
    public static readonly DiagnosticDescriptor PluginUnderReservedPrefix = new(
        id: "ERGOSG015",
        title: "Plugin assembly is excluded from reference scanning by the reserved name prefix",
        messageFormat:
            "Assembly '{0}' declares Ergosfare plugin methods but its name matches the reserved 'Stella.Ergosfare' " +
            "prefix, so reference scanning skipped it and none of its plugin methods are emitted into any pipeline. " +
            "Add [assembly: AssemblyMetadata(\"ErgosfareSourceGeneratorForceScanReferences\", \"true\")] to that " +
            "assembly (or set the same-named MSBuild property in it), or rename it outside the reserved prefix.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    ///     A generic handler or interceptor whose contract names a bare type parameter as
    ///     its message. It registers like any other participant and then binds to nothing:
    ///     participants are matched to messages by concrete type, and a concrete message
    ///     carries no generic arguments to close this participant over.
    /// </summary>
    /// <remarks>
    ///     Deliberately narrow. A generic participant whose contract's message type is built
    ///     from its own type parameters — a handler for a generic message — binds fine and
    ///     draws nothing: the table keys that message by its definition and the dispatch
    ///     closes the participant over the message's own arguments.
    ///     <para>
    ///     A warning rather than an error because the type is legal and may serve some other
    ///     purpose; but a validation or authorization interceptor that silently never runs is
    ///     the worst shape this takes, which is why it is not merely informational.
    ///     </para>
    /// </remarks>
    internal static readonly DiagnosticDescriptor GenericParticipantNeverBinds = new(
        id: "ERGOSG016",
        title: "Generic participant over an open message type binds to nothing and never executes",
        messageFormat:
            "'{0}' takes its message as a type parameter, so no pipeline can bind it: participants are matched to " +
            "messages by concrete type, and a concrete message carries no generic arguments to close this one over. " +
            "It is registered but never runs. Declare the participant over the message type (or a base contract such " +
            "as ICommand) instead. A generic participant for a generic message — one whose contract is built from its " +
            "own type parameters — is unaffected and binds normally.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
