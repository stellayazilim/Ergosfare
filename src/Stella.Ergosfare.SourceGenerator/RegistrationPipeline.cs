using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.Planning;
using Stella.Ergosfare.SourceGenerator.Symbols;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// Turns the generator's collected inputs into the registration file.
/// </summary>
/// <remarks>
/// The discovered models are reconciled into one type list, dispatch reachability is judged
/// over that list, the judged list is planned, and the result is written. The generator
/// itself only wires the providers feeding this.
/// </remarks>
internal static class RegistrationPipeline
{
    /// <summary>
    /// Reports ERGO018 for every <c>Register</c> call naming a type only run time knows.
    /// </summary>
    /// <param name="context">The context diagnostics are reported to.</param>
    /// <param name="registrationSites">The registration calls collected from this compilation.</param>
    /// <remarks>
    /// Every reported call is in this compilation by construction: registration sites come
    /// from its own syntax, and a referenced assembly's manifest carries no location to
    /// report against.
    /// </remarks>
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

    /// <summary>
    /// Runs one registration output: reports the input diagnostics, plans, and adds the
    /// generated source.
    /// </summary>
    /// <param name="context">The context source and diagnostics are added to.</param>
    /// <param name="sourceModels">The registrable types this compilation declares.</param>
    /// <param name="availability">What the referenced Ergosfare package's surface offers.</param>
    /// <param name="referencedModels">The registrable types found in referenced assemblies.</param>
    /// <param name="dispatchSites">The dispatches in this compilation.</param>
    /// <param name="registrationSites">The registration calls in this compilation.</param>
    /// <param name="referencedSites">The dispatch manifests referenced assemblies recorded.</param>
    /// <param name="judgmentInputs">The settings the reachability judgment reads.</param>
    /// <param name="defaultResultAdapterSites">The <c>UseDefaultResultAdapter</c> calls in this compilation.</param>
    /// <param name="pluginInvocations">The plugin methods visible to this compilation.</param>
    /// <remarks>
    /// Nothing is written for a compilation with no registrable type, unless it must still
    /// carry a manifest.
    /// </remarks>
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
        // Reported before anything is planned: a registration this compilation cannot
        // resolve to a type is a defect on its own terms, whatever the rest turns out to
        // look like.
        ReportUnknownRegistrations(context, registrationSites);

        var seen = new HashSet<string>();
        var types = new List<RegistrableTypeModel>();
        var excludedShadows = new List<RegistrableTypeModel>();
        var defaultResultAdapter = ResultAdapterReader.ReduceDefaultResultAdapter(defaultResultAdapterSites);

        // The open definitions monomorphization already answered for. Such a definition
        // still carries IsGenericParticipant on its own model — it is a declared open
        // generic, after all — but it is no longer the shape ERGO016 reports.
        var monomorphizedDefinitions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var model in referencedModels)
        {
            if (model.MonomorphizedFrom is { } definition)
            {
                monomorphizedDefinitions.Add(definition);
            }
        }

        // Source-declared types first: on a full-name collision with a referenced type,
        // typeof in the generated file binds to the source declaration.
        AddModels(context, sourceModels, seen, types, excludedShadows, monomorphizedDefinitions, defaultResultAdapter);
        AddModels(context, referencedModels, seen, types, excludedShadows, monomorphizedDefinitions, defaultResultAdapter);

        // Reachability verdicts and the opt-in handler trim; the returned list is what
        // emission proceeds with.
        types = ErgosfareRegistrationGenerator.ApplyDispatchJudgment(
            context, types, excludedShadows, dispatchSites, registrationSites, referencedSites, judgmentInputs);

        // A compilation declaring no registrable type at all still owes a manifest:
        // otherwise a callsite-only library's dispatches would be invisible to the
        // composition root, and a siteless assembly's marker is exactly what separates
        // "dispatches nothing" from "unknown".
        var emitManifest = availability.HasDispatchSiteAttribute;

        if (types.Count == 0 && !emitManifest)
        {
            return;
        }

        // Deterministic output regardless of declaration or discovery order.
        types.Sort(static (x, y) => string.CompareOrdinal(x.TypeofExpression, y.TypeofExpression));

        var plans = new PlanBuilder(
            types, excludedShadows, availability,
            defaultResultAdapter, pluginInvocations, dispatchSites, referencedSites.Sites).Build();

        var registeredShadows = CollectRootableShadows(excludedShadows);

        var source = RegistrationEmitter.Emit(types, registeredShadows, availability,
            plans.VoidPlans, plans.ResultPlans, plans.StagedPlans, plans.FrozenCompositions,
            emitManifest ? dispatchSites : ImmutableArray<DispatchSiteModel>.Empty,
            emitManifest ? registrationSites : ImmutableArray<RegistrationSiteModel>.Empty,
            emitManifest, GeneratorVersion.Value);
        context.AddSource("ErgosfareRegistrations.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// Selects the hidden messages that are rooted alongside the discovered ones.
    /// </summary>
    /// <param name="excludedShadows">The types hidden from discovery.</param>
    /// <returns>The hidden messages to root, ordered by type name.</returns>
    /// <remarks>
    /// <para>
    /// Rooting is not registering. <c>AddMessage&lt;T&gt;()</c> instantiates a
    /// <c>MessageRoot&lt;T&gt;</c> so a dispatch can close its generic inside a generic
    /// context; it selects nothing into any container, which stays settled by what the
    /// application registered. Without the root the dispatch closes the same generic through
    /// <c>MakeGenericType</c> — an answer only a JIT can give, so the same dispatch works in
    /// development and fails under NativeAOT.
    /// </para>
    /// <para>
    /// So every hidden message this compilation can name is rooted, whether or not anything
    /// registers it: <c>[ExcludeFromDiscovery]</c> keeps a type out of bulk collection, and
    /// says nothing about whether it is dispatched. The cost is one empty object per hidden
    /// message that never is.
    /// </para>
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
            // ExcludeFromDiscovery] said "do not look here at all", which is wider than a
            // type's own "keep me out of bulk registration": its types are not ours to name,
            // and its internals not ours to reach. This compilation's own hidden types are a
            // different matter — it declared them, so it can name them.
            if (shadow is { IsDispatchableMessage: true, IsAccessible: true, ReferencedAssemblyName: null })
            {
                rooted.Add(shadow);
            }
        }

        rooted.Sort(static (x, y) => string.CompareOrdinal(x.TypeofExpression, y.TypeofExpression));

        return rooted;
    }

    /// <summary>
    /// Sorts one batch of models into the registrable list or the hidden one, reporting what
    /// each says about itself along the way.
    /// </summary>
    /// <param name="context">The context diagnostics are reported to.</param>
    /// <param name="models">The models to add.</param>
    /// <param name="seen">The types already added; a repeat is skipped.</param>
    /// <param name="types">The list registrable types are added to.</param>
    /// <param name="excludedShadows">The list types hidden from discovery are added to.</param>
    /// <param name="monomorphizedDefinitions">The open definitions monomorphization closed.</param>
    /// <param name="defaultResultAdapter">The container's default result adapter, if it names one.</param>
    /// <remarks>
    /// Batches are added source-first, so the first model to claim a type name wins.
    /// </remarks>
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
                // A deliberate opt-out: no registration and no diagnostics. The shadow only
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
            // at least one message is not that shape.
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

            // ERGO011/012 are judged where the message is compiled: a referenced message's
            // annotations were already answered for in its own build.
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

            // ERGO013/014: with a default adapter configured, a result-bearing message no
            // tier serves is a throwing pipeline. Unacknowledged that fails the build right
            // here rather than at some later dispatch; acknowledged with
            // [IgnoreResultAdapter] it stays visible as a warning.
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
    /// Walks each model, followed by the event messages its subscriber contracts named.
    /// </summary>
    /// <param name="models">The models to walk.</param>
    /// <returns>Every model and every message carried inside one.</returns>
    /// <remarks>
    /// Those messages travel inside their subscriber's model so the syntax provider keeps
    /// comparing one value per declaration; here they become registrable types in their own
    /// right, and are judged on their own terms — a subscriber may be hidden from discovery
    /// while the message it serves is not. Repeats, whether two subscribers for one message
    /// or one subscriber seen through several partial declarations, are dropped by the
    /// caller's <c>seen</c> set.
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
