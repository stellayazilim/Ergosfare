using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.Planning;
using Stella.Ergosfare.SourceGenerator.Symbols;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     One run of the registration output: reconcile the discovered models into a single
///     type list, judge dispatch reachability over it, plan what the judged list compiles to,
///     and write the file. The generator itself only wires the providers that feed this.
/// </summary>

internal static class RegistrationPipeline
{
    /// <summary>
    ///     ERGO018 at every <c>Register</c> call naming a type only run time knows. Local
    ///     to this compilation by construction: registration sites come from its own syntax,
    ///     and a referenced assembly's manifest carries no location to report against.
    /// </summary>
    private static void ReportUnknownRegistrations(
        SourceProductionContext context, ImmutableArray<RegistrationSiteModel> registrationSites)
    {
        foreach (var site in registrationSites)
        {
            if (site.UnknownTypeLocation is { } location)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.UnknownRegisteredType, location.ToLocation()));
            }
        }
    }

    internal static void Execute(
        SourceProductionContext context,
        ImmutableArray<RegistrableTypeModel> sourceModels,
        ModuleBuilderAvailability availability,
        ImmutableArray<RegistrableTypeModel> referencedModels,
        ImmutableArray<DispatchSiteModel> dispatchSites,
        ImmutableArray<RegistrationSiteModel> registrationSites,
        DispatchManifestScanResult referencedSites,
        JudgmentInputs judgmentInputs,
        ImmutableArray<DefaultResultAdapterSiteModel> defaultResultAdapterSites,
        ImmutableArray<PluginInvocationModel> pluginInvocations)
    {
        // The closed world's entry condition, reported before anything is planned: a
        // registration this compilation cannot resolve to a type is a defect on its own
        // terms, whatever the rest of the compilation turns out to look like.
        ReportUnknownRegistrations(context, registrationSites);

        var seen = new HashSet<string>();
        var types = new List<RegistrableTypeModel>();
        var excludedShadows = new List<RegistrableTypeModel>();
        var defaultResultAdapter = ResultAdapterReader.ReduceDefaultResultAdapter(defaultResultAdapterSites);

        // Source-declared types first: on a (pathological) full-name collision with a
        // referenced type, typeof in the generated file binds to the source declaration.
        // The open definitions monomorphization answered for. A definition that closed over
        // at least one message still carries IsGenericParticipant on its own model — it is a
        // declared open generic, after all — but it is no longer the shape ERGO016 reports.
        var monomorphizedDefinitions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var model in referencedModels)
        {
            if (model.MonomorphizedFrom is { } definition)
            {
                monomorphizedDefinitions.Add(definition);
            }
        }

        AddModels(context, sourceModels, seen, types, excludedShadows, monomorphizedDefinitions, defaultResultAdapter);
        AddModels(context, referencedModels, seen, types, excludedShadows, monomorphizedDefinitions, defaultResultAdapter);

        // Reachability verdicts and the opt-in handler trim; the returned list is what
        // emission proceeds with.
        types = ErgosfareRegistrationGenerator.ApplyDispatchJudgment(
            context, types, excludedShadows, dispatchSites, registrationSites, referencedSites, judgmentInputs);

        // The manifest must be emitted even from a compilation that declares no
        // registrable type at all — a callsite-only library's sites would otherwise be
        // invisible to the composition root, and a siteless assembly's marker is exactly
        // what distinguishes "dispatches nothing" from "unknown".
        var emitManifest = availability.HasDispatchSiteAttribute;

        if (types.Count == 0 && !emitManifest)
        {
            return;
        }

        // Deterministic output regardless of declaration/discovery order.
        types.Sort(static (x, y) => string.CompareOrdinal(x.TypeofExpression, y.TypeofExpression));

        var plans = new PlanBuilder(
            types, excludedShadows, availability,
            defaultResultAdapter, pluginInvocations, dispatchSites, referencedSites.Sites).Build();

        // Hidden messages are rooted like discovered ones: rooting is not registering, and
        // hiding a type from bulk collection does not stop it from being dispatched.
        var registeredShadows = CollectRootableShadows(excludedShadows);

        var source = RegistrationEmitter.Emit(types, registeredShadows, availability,
            plans.VoidPlans, plans.ResultPlans, plans.StagedPlans, plans.FrozenCompositions,
            emitManifest ? dispatchSites : ImmutableArray<DispatchSiteModel>.Empty,
            emitManifest ? registrationSites : ImmutableArray<RegistrationSiteModel>.Empty,
            emitManifest, GeneratorVersion.Value);
        context.AddSource("ErgosfareRegistrations.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    ///     Every hidden message the compilation can name, so it is rooted alongside the
    ///     discovered ones. <c>[ExcludeFromDiscovery]</c> keeps a type out of bulk collection;
    ///     it does not make it invisible, and it does not stop the type from being dispatched.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Rooting is not registering. <c>AddMessage&lt;T&gt;()</c> instantiates a
    ///     <c>MessageRoot&lt;T&gt;</c> so a dispatch can close its generic inside a generic
    ///     context; it selects nothing into any container, which is still settled entirely by
    ///     what the application registered. Without the root the dispatch closes the same
    ///     generic through <c>MakeGenericType</c> — an answer only a JIT can give, so the same
    ///     dispatch works in development and fails under NativeAOT.
    ///     </para>
    ///     <para>
    ///     This used to ask whether a registration named the message, on the reasoning that a
    ///     hand-written <c>Register</c> collects what <c>RegisterGenerated()</c> would. That
    ///     reasoning is about <em>registration</em>, and rooting is not that: the question a
    ///     root answers is whether a dispatch can close its generic, which does not depend on
    ///     who selected what. Measured, the narrower rule left five shapes on the reflective
    ///     arm — the deliberate no-handler fixtures, and a subtype only a base-typed handler
    ///     knows about — none of which a registration can name, and all of which are dispatched.
    ///     </para>
    ///     <para>
    ///     What the wider rule costs is one empty object per hidden message that is never
    ///     dispatched. What it buys is that a dispatch of a type the compilation declared
    ///     never depends on a JIT, which is the whole point of the table.
    ///     </para>
    /// </remarks>
    private static List<RegistrableTypeModel> CollectRootableShadows(
        List<RegistrableTypeModel> excludedShadows)
    {
        if (excludedShadows.Count == 0)
        {
            return [];
        }

        var rooted = new List<RegistrableTypeModel>();

        foreach (var shadow in excludedShadows)
        {
            // Accessibility is emission's own requirement — the root names the type — and a
            // non-dispatchable shape has no generic for a dispatch to close.
            //
            // Source-declared only. A referenced assembly carrying [assembly:
            // ExcludeFromDiscovery] said "do not look here at all", which is a wider
            // statement than a type's own "keep me out of bulk registration": its types are
            // not ours to name, and its internals are not ours to reach. This compilation's
            // own hidden types are a different matter — it declared them, so it can name them.
            if (shadow is { IsDispatchableMessage: true, IsAccessible: true, ReferencedAssemblyName: null })
            {
                rooted.Add(shadow);
            }
        }

        rooted.Sort(static (x, y) => string.CompareOrdinal(x.TypeofExpression, y.TypeofExpression));

        return rooted;
    }

    internal static void AddModels(
        SourceProductionContext context,
        ImmutableArray<RegistrableTypeModel> models,
        HashSet<string> seen,
        List<RegistrableTypeModel> types,
        List<RegistrableTypeModel> excludedShadows,
        HashSet<string> monomorphizedDefinitions,
        DefaultResultAdapterSiteModel? defaultResultAdapter)
    {
        foreach (var model in WithDerivedEventMessages(models))
        {
            if (!seen.Add(model.TypeofExpression))
            {
                continue;
            }

            if (model.IsExcludedFromDiscovery)
            {
                // Deliberate opt-out: no registration, no diagnostics — the shadow only
                // feeds the reachability judgment's exclusion zone.
                excludedShadows.Add(model);
                continue;
            }

            if (!model.IsAccessible)
            {
                context.ReportDiagnostic(model.ReferencedAssemblyName is { } referencedAssembly
                    ? Diagnostic.Create(
                        GeneratorDiagnostics.InvisibleReferencedRegistrableType,
                        location: null,
                        model.DisplayName,
                        referencedAssembly)
                    : Diagnostic.Create(
                        GeneratorDiagnostics.InaccessibleRegistrableType,
                        model.Location?.ToLocation(),
                        model.DisplayName));
                continue;
            }

            // Ahead of the informational ones: a participant that never runs makes every
            // finding about how it would be constructed moot. A definition that closed over
            // at least one message is not that shape — monomorphization answered for it.
            if (model.IsGenericParticipant && !monomorphizedDefinitions.Contains(model.TypeofExpression))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.GenericParticipantNeverBinds,
                    model.InfoLocation?.ToLocation(),
                    model.DisplayName));
            }

            if (model.HasMultiplePublicConstructors)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.MultiplePublicConstructors,
                    model.InfoLocation?.ToLocation(),
                    model.DisplayName));
            }

            if (model.HasFromServicesConstructorParameter)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.FromServicesOnConstructor,
                    model.InfoLocation?.ToLocation(),
                    model.DisplayName));
            }

            // ERGO011/012 judge where the message is compiled: a referenced message's
            // annotations were already judged (or predate the rules) in its own build.
            if (model.ReferencedAssemblyName is null && model.ResultAdapter is { } resultAdapter)
            {
                if (model.HasIgnoredResultAdapter)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GeneratorDiagnostics.ConflictingResultAdapterAnnotations,
                        model.InfoLocation?.ToLocation(),
                        model.DisplayName));
                }
                else if (!resultAdapter.IsInstantiable || !resultAdapter.FitsDeclaredSlot)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GeneratorDiagnostics.UnbindableResultAdapter,
                        model.InfoLocation?.ToLocation(),
                        resultAdapter.DisplayName,
                        model.DisplayName,
                        resultAdapter.IsInstantiable
                            ? "it does not implement IResultAdapter<TResult> for any result slot the message dispatches"
                            : "the runtime binding cannot instantiate it — a concrete, fully closed type with a " +
                              "public parameterless constructor is required"));
                }
            }

            // ERGO013/014: with a default adapter configured, a result-bearing message
            // no tier serves stays a throwing pipeline. Unacknowledged, that is a design
            // hole and fails the build right here — no reason to wait for a dispatch to
            // reveal it; acknowledged via [IgnoreResultAdapter], it stays visible as a
            // warning.
            if (model.ReferencedAssemblyName is null
                && defaultResultAdapter is not null
                && model.IsDispatchableMessage
                && model.ResultAdapter is null
                && ResultAdapterReader.HasUnservedResultSlots(model, defaultResultAdapter, out var unservedSlot))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    model.HasIgnoredResultAdapter
                        ? GeneratorDiagnostics.AcknowledgedThrowingPipeline
                        : GeneratorDiagnostics.UnservedByDefaultResultAdapter,
                    model.InfoLocation?.ToLocation(),
                    model.DisplayName,
                    unservedSlot,
                    defaultResultAdapter.BaseTypeExpression));
            }

            types.Add(model);
        }
    }

    /// <summary>
    ///     Each model, followed by the event messages its subscriber contracts named. The
    ///     messages travel inside their subscriber's model so the syntax provider keeps
    ///     comparing one value per declaration; this is where they become registrable types
    ///     in their own right.
    /// </summary>
    /// <remarks>
    ///     A derived message is emitted on its own terms rather than its subscriber's: the
    ///     subscriber may be hidden from discovery and the message still is not, because
    ///     hiding a subscriber says nothing about the message it serves. Duplicates — two
    ///     subscribers for one message, or a subscriber seen twice through partial
    ///     declarations — are dropped by the caller's <c>seen</c> set.
    /// </remarks>
    private static IEnumerable<RegistrableTypeModel> WithDerivedEventMessages(
        ImmutableArray<RegistrableTypeModel> models)
    {
        foreach (var model in models)
        {
            yield return model;

            foreach (var derived in model.DerivedEventMessages)
            {
                yield return derived;
            }
        }
    }
}
