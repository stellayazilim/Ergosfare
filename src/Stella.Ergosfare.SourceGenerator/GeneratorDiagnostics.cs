using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     Diagnostics reported by the Ergosfare source generator (ERGO prefix).
/// </summary>
internal static class GeneratorDiagnostics
{
    /// <summary>
    ///     A type carries an Ergosfare marker interface but generated code cannot reference
    ///     it, so it is silently invisible to source-generated registration — worth a
    ///     warning rather than silent divergence from what the declaration promises.
    /// </summary>
    public static readonly DiagnosticDescriptor InaccessibleRegistrableType = new(
        id: "ERGO001",
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
        id: "ERGO002",
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
        id: "ERGO003",
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
        id: "ERGO004",
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
        id: "ERGO005",
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
        id: "ERGO006",
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
        id: "ERGO007",
        title: "No dispatch site can reach this handler",
        messageFormat:
            "Handler '{0}' handles '{1}', but no dispatch site in the compiled closure can deliver that " +
            "message{2} — the handler never executes. Suppress ERGO007 if the registration is deliberate, or " +
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
        id: "ERGO008",
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
    ///     (<c>dotnet_diagnostic.ERGO009.severity = warning</c>).
    /// </summary>
    public static readonly DiagnosticDescriptor OpaqueDispatchSite = new(
        id: "ERGO009",
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
        id: "ERGO010",
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
        id: "ERGO011",
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
        id: "ERGO012",
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
    ///     throwing pipeline and downgrades the finding to ERGO014.
    /// </summary>
    public static readonly DiagnosticDescriptor UnservedByDefaultResultAdapter = new(
        id: "ERGO013",
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
    ///     The acknowledged twin of ERGO013: the message opted out via
    ///     <c>[IgnoreResultAdapter]</c> while the application's default result adapter
    ///     cannot serve its result type anyway — a deliberate throwing pipeline inside a
    ///     value-channel application, kept visible as a warning. Suppressible through the
    ///     standard channels when the acknowledgment itself is considered enough.
    /// </summary>
    public static readonly DiagnosticDescriptor AcknowledgedThrowingPipeline = new(
        id: "ERGO014",
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
        id: "ERGO015",
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
        id: "ERGO016",
        title: "Generic participant closes over no message and never executes",
        messageFormat:
            "'{0}' takes its message as a type parameter and no compiled message satisfies its constraint, so it " +
            "closes over nothing and no pipeline contains it — it is registered but never runs. Give the constraint a " +
            "message that implements it, or declare the participant over the message type (or a base contract such as " +
            "ICommand) instead. A participant whose constraint some message does satisfy is closed over each of them " +
            "automatically, and a generic participant for a generic message binds on its own.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    ///     A plugin declares an options type, but one of its services has no way to receive
    ///     it: no constructor to pass it to, and not partial, so the generator cannot write
    ///     one either.
    /// </summary>
    /// <remarks>
    ///     The options instance never enters the container — the module holds it and passes
    ///     it to the constructed service — so a service the generator cannot construct with
    ///     it simply never sees it. Reported rather than left alone because the plugin's
    ///     author declared settings and this service silently ignores them.
    /// </remarks>
    internal static readonly DiagnosticDescriptor PluginServiceCannotReceiveOptions = new(
        id: "ERGO017",
        title: "Plugin service cannot receive the plugin's options",
        messageFormat:
            "'{0}' carries plugin hook methods and its plugin declares options of type '{1}', but the service has no " +
            "constructor to receive them and is not declared partial, so none reaches it. Declare a constructor taking " +
            "'{1}' (other parameters are resolved from the container), or mark the class partial and the generator " +
            "writes the field and constructor for you.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    ///     A <c>Register</c> call whose type is decided at run time — a <c>Type</c>-valued
    ///     argument that is not a <c>typeof</c> literal, or a type argument that is itself a
    ///     type parameter. Ergosfare's world is closed: a construct's pipeline is compiled,
    ///     its composition frozen and its plan baked from what this compilation can see. A
    ///     type named only at run time enters none of that, so the registration cannot mean
    ///     what it appears to mean.
    /// </summary>
    /// <remarks>
    ///     An error rather than a suppression of the reachability judgment, which is what it
    ///     used to be: a build that cannot say which types it registers cannot be told
    ///     anything useful about its dispatches either, and the honest place to say so is
    ///     the registration, not the dispatch that later looks dead.
    /// </remarks>
    internal static readonly DiagnosticDescriptor UnknownRegisteredType = new(
        id: "ERGO018",
        title: "Registered type is not known at compile time",
        messageFormat:
            "This registration names its type at run time, which the closed-world model cannot follow — the type gets " +
            "no compiled pipeline, no frozen composition and no plan. Register it as 'Register(typeof(T))' or " +
            "'Register<T>()' with a concrete type, or let 'RegisterGenerated()' collect it.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
