using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// The diagnostics the Ergosfare source generator reports, under the <c>ERGO</c> prefix.
/// </summary>
internal static class GeneratorDiagnostics
{
    /// <summary>
    /// ERGO001: a type carries an Ergosfare marker but generated code cannot name it.
    /// </summary>
    /// <remarks>
    /// A private, protected or file-local type is invisible to generated registration. A
    /// warning, because the declaration promises something its accessibility takes back.
    /// </remarks>
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
    /// ERGO002: a marker type in a scanned assembly is not visible to this compilation.
    /// </summary>
    /// <remarks>
    /// Internal without an <c>InternalsVisibleTo</c> grant, nested behind a private or
    /// protected level, or compiler-mangled. The same promise as ERGO001, made from across an
    /// assembly boundary.
    /// </remarks>
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
    /// ERGO003: a handler with several public constructors stays on the container path.
    /// </summary>
    /// <remarks>
    /// Which constructor the container picks depends on what is registered, so the plan
    /// cannot prove direct construction would do the same thing. Informational: everything
    /// still works, only the construction fast path is lost.
    /// </remarks>
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
    /// ERGO004: <c>[FromServices]</c> sits on a constructor parameter, where it does nothing.
    /// </summary>
    /// <remarks>
    /// It is an ASP.NET Core action-parameter attribute; constructor injection resolves
    /// services with or without it. Informational, so the stray attribute does not read as
    /// behavior that is not there.
    /// </remarks>
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
    /// ERGO005: a dispatch that can never reach a handler.
    /// </summary>
    /// <remarks>
    /// Neither the site's static message type nor any of its subtypes in the compiled closure
    /// has a covering registration, so whatever instance the expression carries, the dispatch
    /// throws. Reported only where the closure is complete — a composition root. An error:
    /// nothing should compile around a provably dead dispatch.
    /// </remarks>
    public static readonly DiagnosticDescriptor DeadDispatch = new(
        id: "ERGO005",
        title: "Dispatch can never reach a handler",
        messageFormat:
            "This dispatch of '{0}' can never reach a handler: neither '{0}' nor any of its subtypes in the " +
            "compiled closure has a covering handler registration, so {1}{2}. Register a handler for it or " +
            "remove the dispatch.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ERGO006: only subtypes of a dispatched type are handled.
    /// </summary>
    /// <remarks>
    /// The site's static type is a concrete message with no covering handler while some
    /// subtype has one, so an instance of exactly that type throws where a subtype instance
    /// would succeed.
    /// </remarks>
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
    /// ERGO007: no dispatch in the compiled closure can reach a handler.
    /// </summary>
    /// <remarks>
    /// No recorded site's static type, and no closure subtype of one, is covered by the
    /// handler's registration, so it never runs. Judged in a composition root, and only when
    /// every Ergosfare-referencing assembly in the closure carries a manifest — without that,
    /// sites may be invisible and the judgment stays silent.
    /// </remarks>
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
    /// ERGO008: an unreachable handler was left out of the generated registration.
    /// </summary>
    /// <remarks>
    /// What <c>ErgosfareTrimUnusedHandlers</c> did: no descriptor, no plan and no dispatch
    /// root for the handler, which lets the linker drop it. Informational, so the exclusion
    /// is visible in the build log.
    /// </remarks>
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
    /// ERGO009: a dispatch site's static message type says nothing about what it dispatches.
    /// </summary>
    /// <remarks>
    /// A bare module marker, <c>IMessage</c>, <c>object</c> or an unconstrained type
    /// parameter leaves reachability provable only closure-wide. Off by default; a team that
    /// wants every dispatch provable raises it through .editorconfig with
    /// <c>dotnet_diagnostic.ERGO009.severity = warning</c>.
    /// </remarks>
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
    /// ERGO010: several main handlers claim one message at the same level.
    /// </summary>
    /// <remarks>
    /// Several direct handlers, or several covariant ones with no direct handler to beat
    /// them. The priority ladder has no tiebreaker inside a level, so every dispatch of that
    /// message throws. An error, because it is provable now. Judged over default-discovery,
    /// ungrouped handlers in a composition root: a keyed or grouped registration is a
    /// container choice, and nothing here can prove two of them are ever live together.
    /// </remarks>
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
    /// ERGO011: a <c>[ResultAdapter]</c> annotation names an adapter that can never bind.
    /// </summary>
    /// <remarks>
    /// It implements <c>IResultAdapter&lt;TResult&gt;</c> for no slot the message dispatches,
    /// or the runtime binding cannot instantiate it. An error: the annotation promises
    /// value-channel semantics the pipeline would never deliver, or would crash delivering.
    /// </remarks>
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
    /// ERGO012: a message both declares a result adapter and opts out of adapters.
    /// </summary>
    /// <remarks>
    /// <c>[ResultAdapter]</c> and <c>[IgnoreResultAdapter]</c> together, in any mix of own
    /// and inherited. An error: the contradiction has no resolution to pick.
    /// </remarks>
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
    /// ERGO013: a default result adapter is configured, and this message's result reaches no
    /// adapter at all.
    /// </summary>
    /// <remarks>
    /// Failures in that pipeline cannot travel as values and surface as thrown exceptions. An
    /// error, and reached at compile time rather than at some later dispatch: configuring a
    /// default makes value-based error handling the application's contract, and a message
    /// that quietly cannot take part in it is a hole in that contract. The per-message escape
    /// is <c>[IgnoreResultAdapter]</c>, which turns this into ERGO014.
    /// </remarks>
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
    /// ERGO014: a message opted out of result adaptation and keeps a throwing pipeline.
    /// </summary>
    /// <remarks>
    /// The acknowledged form of ERGO013 — a deliberate throwing pipeline inside a
    /// value-channel application, kept visible as a warning, and suppressible through the
    /// usual channels when the acknowledgment is considered enough on its own.
    /// </remarks>
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
    /// ERGO015: a plugin assembly is skipped because its name carries the reserved prefix.
    /// </summary>
    /// <remarks>
    /// The exclusion is right for Ergosfare's own assemblies; for a plugin package named
    /// under the prefix it means the plugin reaches no plan at all, silently — the worst
    /// failure a plugin ecosystem can have, so it is reported.
    /// </remarks>
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
    /// ERGO016: a generic participant closes over no message and never runs.
    /// </summary>
    /// <remarks>
    /// Its contract names a bare type parameter as the message, so it registers like any
    /// other participant and then binds to nothing: participants are matched by concrete
    /// type, and a concrete message carries no arguments to close it over.
    /// <para>
    /// Narrow by design. A generic participant whose contract builds its message from its own
    /// type parameters — a handler for a generic message — binds fine and draws nothing here.
    /// A warning rather than an error, because the type is legal and may serve another
    /// purpose; not merely informational, because a validation or authorization interceptor
    /// that quietly never runs is the worst shape this takes.
    /// </para>
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
    /// ERGO017: a plugin service has no way to receive its plugin's options.
    /// </summary>
    /// <remarks>
    /// No constructor to pass them to, and not partial, so the generator cannot write one
    /// either. The options never enter the container — the module holds them and hands them
    /// to the service it constructs — so a service that cannot be constructed with them never
    /// sees them. Reported because the plugin's author declared settings this service ignores.
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
    /// ERGO018: a <c>Register</c> call names its type at run time.
    /// </summary>
    /// <remarks>
    /// A <c>Type</c>-valued argument that is not a <c>typeof</c> literal, or a type argument
    /// that is itself a type parameter. Ergosfare's world is closed: a construct's pipeline
    /// is compiled, its composition frozen and its plan baked from what this compilation can
    /// see, and a type named only at run time enters none of that. An error at the
    /// registration rather than a silenced judgment at the dispatch that later looks dead —
    /// a build that cannot say which types it registers cannot be told anything useful about
    /// its dispatches either.
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

    /// <summary>
    /// ERGO019: a <c>UseDefaultResultAdapter</c> call names its adapter with something other
    /// than a literal <c>typeof</c>.
    /// </summary>
    /// <remarks>
    /// A variable, a conditional, a type that does not resolve, or a definition nested inside
    /// a generic type. An error: which result types the fallback serves — and what closes an
    /// open definition over each of them — is decided here and written into the generated
    /// table, and a call this cannot read leaves every pipeline in the application without a
    /// fallback with nothing to say so.
    /// </remarks>
    public static readonly DiagnosticDescriptor OpaqueDefaultResultAdapter = new(
        id: "ERGO019",
        title: "Default result adapter is not named at compile time",
        messageFormat:
            "This UseDefaultResultAdapter call does not name its adapter with a literal typeof, so the fallback it " +
            "configures reaches no compiled pipeline. Name the adapter directly — 'UseDefaultResultAdapter(" +
            "typeof(MyAdapter))', or 'typeof(MyAdapter<>)' for an open definition — declared outside any generic " +
            "type.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ERGO020: a compilation configures two different default result adapters.
    /// </summary>
    /// <remarks>
    /// The fallback is the application's answer for every result type nothing more specific
    /// serves, and a message's pipeline is compiled once. Two answers leave no way to say
    /// which one a given dispatch was compiled against, so the disagreement is settled here
    /// rather than by whichever container happened to be built.
    /// </remarks>
    public static readonly DiagnosticDescriptor ConflictingDefaultResultAdapters = new(
        id: "ERGO020",
        title: "Conflicting default result adapters",
        messageFormat:
            "This UseDefaultResultAdapter call names '{0}', and another in the same compilation names '{1}' — a " +
            "compilation configures one fallback adapter, because the pipelines compiled from it are compiled once. " +
            "Name one adapter, or have it serve both result families through several " +
            "IResultAdapter<TResult> implementations.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ERGO021: the configured default result adapter cannot be named by generated code.
    /// </summary>
    /// <remarks>
    /// Inaccessible from this compilation, abstract, without a public parameterless
    /// constructor, or otherwise unspellable. An error for the same reason ERGO019 is: the
    /// generated table is where the fallback's answers live, and an adapter that cannot be
    /// written into it has no other way to reach a dispatch.
    /// </remarks>
    public static readonly DiagnosticDescriptor UnbakeableDefaultResultAdapter = new(
        id: "ERGO021",
        title: "Default result adapter cannot be constructed by generated code",
        messageFormat:
            "The default result adapter '{0}' cannot be named and constructed by the generated registration: a " +
            "concrete type, accessible from this compilation and with a public parameterless constructor, is " +
            "required.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ERGO022: a message carrying a chunk channel is published.
    /// </summary>
    /// <remarks>
    /// A publish delivers to every subscriber; a chunk channel is consumed once. Whichever
    /// subscriber reads it first takes the payload and the rest get an empty sequence — and
    /// which one that is depends on registration order. An error rather than a warning: there
    /// is no arrangement of subscribers that makes it work, so nothing is lost by refusing it
    /// here.
    /// </remarks>
    public static readonly DiagnosticDescriptor PublishedStreamMessage = new(
        id: "ERGO022",
        title: "Stream message is published",
        messageFormat:
            "Message '{0}' carries a chunk channel, and a publish delivers it to every subscriber — but the channel " +
            "is consumed once, so whichever subscriber reads first takes the payload and the others see nothing. " +
            "Send it to a single handler, or give each subscriber its own stream.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
