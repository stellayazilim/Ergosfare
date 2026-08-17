
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.ResultAdapters;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// Writes the generated registration source.
/// </summary>
/// <remarks>
/// <para>
/// The emitted code is plain, conservative C# — a block namespace, no target-typed
/// constructs — because it compiles inside the consumer's project under whatever language
/// version they use.
/// </para>
/// <para>
/// Every registration surface comes in two overloads. The one taking no pattern selects
/// default discovery, the types carrying no <c>[DiscoveryKey]</c>; the one taking a pattern
/// picks keyed types, as in <c>RegisterGenerated("reporting.*")</c>. Types are written in
/// clusters sharing a discovery-key set, each behind a key-match test, and selection is a
/// union, so overlapping selections across chained calls are safe.
/// </para>
/// <para>
/// Registration names constructs and nothing more: what each one's pipeline looks like was
/// decided at compile time and lives in the frozen composition table. A message goes through
/// <c>Register(typeof(T))</c>, and participants are batched into one
/// <c>RegisterParticipants</c> call. Every type is named through <c>typeof</c>, which also
/// leaves the trimming and AOT analyzers with nothing to complain about.
/// </para>
/// </remarks>
internal static class RegistrationEmitter
{
    private const string CatalogFullName = "global::Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenCompositionCatalog";
    private const string CommandBuilderFullName = "global::Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder";
    private const string QueryBuilderFullName = "global::Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder";
    private const string EventBuilderFullName = "global::Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder";
    private const string DispatchRootsFullName = "global::Stella.Ergosfare.Core.Abstractions.DispatchRoots.GeneratedDispatchRoots";
    private const string DispatchSiteAttributeFullName = "global::Stella.Ergosfare.Core.Abstractions.DispatchSites.DispatchSiteAttribute";
    private const string DispatchManifestAttributeFullName = "global::Stella.Ergosfare.Core.Abstractions.DispatchSites.DispatchManifestAttribute";
    private const string ManualRegistrationAttributeFullName = "global::Stella.Ergosfare.Core.Abstractions.DispatchSites.ManualRegistrationAttribute";
    private const string DispatchKindFullName = "global::Stella.Ergosfare.Core.Abstractions.DispatchSites.DispatchKind";

    /// <summary>
    /// The manifest schema version stamped into the assembly marker.
    /// </summary>
    private const int DispatchManifestVersion = 1;

    /// <summary>
    /// Writes the <c>ErgosfareGeneratedRegistrations</c> class for the discovered types.
    /// </summary>
    /// <param name="types">The types to register.</param>
    /// <param name="registeredShadows">The hidden messages to root.</param>
    /// <param name="builders">What the referenced Ergosfare package's surface offers.</param>
    /// <param name="voidPlans">The single-handler plans for void pipelines.</param>
    /// <param name="resultPlans">The single-handler plans for result pipelines.</param>
    /// <param name="stagedPlans">The compiled pipeline bodies.</param>
    /// <param name="frozenCompositions">The baked composition table.</param>
    /// <param name="dispatchSites">The dispatches to record in the manifest.</param>
    /// <param name="registrationSites">The registrations to record in the manifest.</param>
    /// <param name="defaultResultAdapter">The container's fallback result adapter, if it names one.</param>
    /// <param name="defaultResultAdapter">The container's fallback result adapter, if it names one.</param>
    /// <param name="emitDispatchManifest">Whether this assembly carries a manifest.</param>
    /// <param name="generatorVersion">The version stamped on the generated code.</param>
    /// <returns>The generated source.</returns>
    public static string Emit(
        IReadOnlyList<RegistrableTypeModel> types,
        IReadOnlyList<RegistrableTypeModel> registeredShadows,
        ModuleBuilderAvailability builders,
        IReadOnlyList<VoidPlanModel> voidPlans,
        IReadOnlyList<ResultPlanModel> resultPlans,
        IReadOnlyList<StagedPlanModel> stagedPlans,
        IReadOnlyList<FrozenCompositionModel> frozenCompositions,
        IReadOnlyList<DispatchSiteModel> dispatchSites,
        IReadOnlyList<RegistrationSiteModel> registrationSites,
        DefaultResultAdapterSiteModel? defaultResultAdapter,
        bool emitDispatchManifest,
        string generatorVersion)
    {
        var emitDispatchRoots = builders.HasDispatchRoots
                                && (HasDispatchableMessages(types) || registeredShadows.Count > 0);
        var sb = new StringBuilder(8192);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by Stella.Ergosfare.SourceGenerator — do not edit.");
        sb.AppendLine("// Compile-time registration of the discovered Ergosfare constructs — no reflection-based assembly scanning.");
        // Annotations on, warnings off. Annotations have to be enabled because the emitted
        // code writes `T?` wherever a contract declares it. Warnings must not be: this file
        // lands in someone else's compilation, under their nullable settings and possibly
        // their TreatWarningsAsErrors, and every nullability warning it could raise would be
        // about code they cannot edit. Generic argument positions are where that bites — a
        // handler whose result is a nullable reference type, `IQueryHandler<Q, TodoDto?>`,
        // does not satisfy a constraint written against the unannotated type, and CS8631
        // would fail the consumer's build.
        sb.AppendLine("#nullable enable annotations");
        sb.AppendLine("#nullable disable warnings");
        // Registration names every discovered construct, deprecated ones included, and
        // surfacing their [Obsolete] here would warn about code nobody wrote.
        sb.AppendLine("#pragma warning disable CS0612, CS0618");
        sb.AppendLine();

        if (emitDispatchManifest)
        {
            EmitDispatchManifest(sb, dispatchSites, registrationSites);
        }

        sb.AppendLine("namespace Stella.Ergosfare.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    ///     Ergosfare constructs (messages, handlers, interceptors) discovered at compile");
        sb.AppendLine("    ///     time in this compilation and its scanned references. Handler descriptors are");
        sb.AppendLine("    ///     pre-computed, so registration performs no reflection over handler types; plain");
        sb.AppendLine("    ///     messages and open generic types register through the runtime fallback.");
        sb.AppendLine("    ///     Trimmed/AOT builds stay warning-free: every type is referenced statically via");
        sb.AppendLine("    ///     <c>typeof</c>.");
        sb.AppendLine("    /// </summary>");
        sb.Append("    [global::System.CodeDom.Compiler.GeneratedCode(\"Stella.Ergosfare.SourceGenerator\", \"")
          .Append(generatorVersion)
          .AppendLine("\")]");
        sb.AppendLine("    internal static class ErgosfareGeneratedRegistrations");
        sb.AppendLine("    {");

        var wroteMember = false;

        if (builders.HasCompositionCatalog)
        {
            EmitCatalogSurface(sb, ref wroteMember, types, emitDispatchRoots);
        }

        if (builders.HasCommandModuleBuilder)
        {
            EmitBuilderSurface(sb, ref wroteMember, CommandBuilderFullName, "command",
                Filter(types, static t => t.IsCommand), builders.CommandBuilderHasRegisterParticipants,
                emitDispatchRoots);
        }

        if (builders.HasQueryModuleBuilder)
        {
            EmitBuilderSurface(sb, ref wroteMember, QueryBuilderFullName, "query",
                Filter(types, static t => t.IsQuery), builders.QueryBuilderHasRegisterParticipants,
                emitDispatchRoots);
        }

        if (builders.HasEventModuleBuilder)
        {
            EmitBuilderSurface(sb, ref wroteMember, EventBuilderFullName, "event",
                Filter(types, static t => t.IsEvent), builders.EventBuilderHasRegisterParticipants,
                emitDispatchRoots);
        }

        if (emitDispatchRoots)
        {
            EmitDispatchRoots(sb, ref wroteMember, types, registeredShadows, voidPlans, resultPlans, stagedPlans,
                builders.DispatchRootsHasPlanFactories,
                builders.DispatchRootsHasProviderPlanFactories,
                builders.HasKeyedServiceExtensions,
                defaultResultAdapter);

            EmitStagedPlanClasses(sb, ref wroteMember, stagedPlans, builders.StagedPlansSupportDirectConstruction);
        }

        // A separate question from the roots above. That one asks whether this compilation
        // declares a message worth closing generics for; the table says what serves a
        // message, including the abstract bases a dispatched subtype resolves through, which
        // are never rooted themselves.
        if (builders.HasDispatchRoots)
        {
            EmitFrozenCompositions(sb, ref wroteMember, frozenCompositions);
        }

        EmitMatchesHelper(sb, ref wroteMember);

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Writes the assembly-level dispatch manifest.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="dispatchSites">The dispatches this compilation makes.</param>
    /// <param name="registrationSites">The registrations this compilation makes.</param>
    /// <remarks>
    /// The marker attribute is written even with no site at all — it is what separates
    /// "dispatches nothing" from "unknown", and it carries the opaque-registration flag —
    /// followed by one site attribute per distinct message, surface and opacity, and one
    /// registration attribute per named registered type. A referenced assembly's entries are
    /// never re-exported: each assembly records only what it does itself.
    /// </remarks>
    private static void EmitDispatchManifest(
        StringBuilder sb,
        IReadOnlyList<DispatchSiteModel> dispatchSites,
        IReadOnlyList<RegistrationSiteModel> registrationSites)
    {
        var hasOpaqueRegistrations = false;

        foreach (var registration in registrationSites)
        {
            if (registration.IsOpaque)
            {
                hasOpaqueRegistrations = true;
                break;
            }
        }

        sb.Append("[assembly: ").Append(DispatchManifestAttributeFullName)
          .Append('(').Append(DispatchManifestVersion);

        if (hasOpaqueRegistrations)
        {
            sb.Append(", HasOpaqueRegistrations = true");
        }

        sb.AppendLine(")]");

        if (dispatchSites.Count > 0)
        {
            var entries = new List<(string MetadataName, DispatchSiteKind Kind, bool Opaque, string GroupKey, ImmutableArray<string> Groups)>(dispatchSites.Count);
            var seen = new HashSet<(string, DispatchSiteKind, bool, string)>();

            foreach (var site in dispatchSites)
            {
                // A site whose set could not be read records no groups: the assembly reading
                // this back must not take "unreadable" for "the default group".
                var groups = site.HasUnprovableGroups ? ImmutableArray<string>.Empty : site.Groups;
                var entry = (site.MessageTypeMetadataName, site.Kind, site.IsOpaque, GroupKey: JoinGroups(groups));

                if (seen.Add(entry))
                {
                    entries.Add((entry.MessageTypeMetadataName, entry.Kind, entry.IsOpaque, entry.GroupKey, groups));
                }
            }

            // The same output whatever order the calls were found in.
            entries.Sort(static (x, y) =>
            {
                var byName = string.CompareOrdinal(x.MetadataName, y.MetadataName);

                if (byName != 0)
                {
                    return byName;
                }

                var byGroups = string.CompareOrdinal(x.GroupKey, y.GroupKey);

                if (byGroups != 0)
                {
                    return byGroups;
                }

                var byKind = x.Kind.CompareTo(y.Kind);

                return byKind != 0 ? byKind : x.Opaque.CompareTo(y.Opaque);
            });

            foreach (var (metadataName, kind, opaque, _, groups) in entries)
            {
                sb.Append("[assembly: ").Append(DispatchSiteAttributeFullName).Append('(')
                  .Append(SymbolDisplay.FormatLiteral(metadataName, quote: true)).Append(", ")
                  .Append(DispatchKindFullName).Append('.').Append(DispatchKindMemberName(kind)).Append(", ")
                  .Append(opaque ? "true" : "false");

                if (!groups.IsEmpty)
                {
                    sb.Append(", Groups = new string[] { ");

                    for (var i = 0; i < groups.Length; i++)
                    {
                        sb.Append(i == 0 ? string.Empty : ", ")
                          .Append(SymbolDisplay.FormatLiteral(groups[i], quote: true));
                    }

                    sb.Append(" }");
                }

                sb.AppendLine(")]");
            }
        }

        if (registrationSites.Count > 0)
        {
            var registeredNames = new List<string>(registrationSites.Count);
            var seenNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var registration in registrationSites)
            {
                if (registration.TypeMetadataName is { } name && seenNames.Add(name))
                {
                    registeredNames.Add(name);
                }
            }

            registeredNames.Sort(StringComparer.Ordinal);

            foreach (var name in registeredNames)
            {
                sb.Append("[assembly: ").Append(ManualRegistrationAttributeFullName).Append('(')
                  .Append(SymbolDisplay.FormatLiteral(name, quote: true)).AppendLine(")]");
            }
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Flattens a normalized group set into one comparable string.
    /// </summary>
    /// <param name="groups">The set to flatten.</param>
    /// <returns>The joined names.</returns>
    /// <remarks>
    /// Deduplication and sorting already happened, so joining is enough to make two spellings
    /// of one set compare equal.
    /// </remarks>
    private static string JoinGroups(ImmutableArray<string> groups)
    {
        if (groups.IsEmpty)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        for (var i = 0; i < groups.Length; i++)
        {
            // The unit separator keeps {"ab"} apart from {"a","b"}; a group name carrying a
            // control character is not a name anyone writes.
            sb.Append(i == 0 ? string.Empty : "").Append(groups[i]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes a filtering plan's unfiltered entry point.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="isVoid">Whether its pipeline produces no result.</param>
    /// <param name="direct">Whether this is the directly constructed entry point.</param>
    /// <remarks>
    /// The base class declares it either way, so it forwards to the filtered body with the
    /// empty set: a dispatch naming no group is asking for the default one, which is exactly
    /// what an empty request means to every baked test.
    /// </remarks>
    private static void AppendUnfilteredForward(StringBuilder sb, StagedPlanModel plan, bool isVoid, bool direct)
    {
        if (!plan.IsGroupFiltering)
        {
            return;
        }

        var name = direct ? "ExecuteDirect" : "Execute";
        var target = direct ? "ExecuteFilteredDirect" : "ExecuteFiltered";

        sb.AppendLine();
        sb.Append("            public override ").Append(ValueTaskFullName);

        if (!isVoid)
        {
            sb.Append('<').Append(plan.ResultTypeExpression).Append('>');
        }

        sb.Append(' ').Append(name).AppendLine("(");
        sb.Append("                ").Append(plan.MessageTypeExpression).AppendLine(" message,");
        sb.Append("                ").Append(ExecutionContextFullName).AppendLine(" context,");
        sb.AppendLine("                global::System.IServiceProvider serviceProvider)");
        sb.AppendLine("            {");
        sb.Append("                return ").Append(target)
          .AppendLine("(message, context, serviceProvider, global::System.Array.Empty<string>());");
        sb.AppendLine("            }");
    }

    /// <summary>
    /// Opens the <c>if</c> a guarded participant sits behind.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="guard">The participant's group test, or <c>null</c> when it needs none.</param>
    /// <param name="indent">The indent the surrounding body is written at.</param>
    /// <returns>The indent the participant's call is written at.</returns>
    /// <remarks>
    /// An unguarded call — every call of a plan compiled for a known set — is written where
    /// it stands, so both plan shapes go through one path.
    /// </remarks>
    private static string OpenGuard(StringBuilder sb, string? guard, string indent)
    {
        if (guard is null)
        {
            return indent;
        }

        sb.Append(indent).Append("if (").Append(guard).AppendLine(")");
        sb.Append(indent).AppendLine("{");

        return indent + "    ";
    }

    /// <summary>
    /// Closes the <c>if</c> a guarded participant sits behind.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="guard">The guard that was opened, or <c>null</c> when there was none.</param>
    /// <param name="indent">The indent the <c>if</c> was opened at.</param>
    private static void CloseGuard(StringBuilder sb, string? guard, string indent)
    {
        if (guard is not null)
        {
            sb.Append(indent).AppendLine("}");
        }
    }

    /// <summary>
    /// Closes an execute signature.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <remarks>
    /// A filtering plan takes the requested set as a parameter, deciding participation being
    /// what its body does.
    /// </remarks>
    private static void AppendGroupsParameter(StringBuilder sb, StagedPlanModel plan)
    {
        if (plan.IsGroupFiltering)
        {
            sb.AppendLine(",");
            sb.AppendLine("                global::System.Collections.Generic.IReadOnlyList<string> groups)");
        }
        else
        {
            sb.AppendLine(")");
        }
    }

    /// <summary>
    /// Writes the group tests a filtering plan runs once, before any participant.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="indent">The indent to write them at.</param>
    /// <remarks>
    /// One local per distinct declared set, read by every call behind it.
    /// </remarks>
    private static void EmitGroupGuards(StringBuilder sb, StagedPlanModel plan, string indent)
    {
        foreach (var guard in plan.GroupGuards)
        {
            sb.Append(indent).Append("var ").Append(guard.Name).Append(" = ")
              .Append(guard.Expression).AppendLine(";");
        }

        if (!plan.GroupGuards.IsEmpty)
        {
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Names the <c>DispatchKind</c> member a dispatch surface corresponds to.
    /// </summary>
    /// <param name="kind">The dispatch surface.</param>
    /// <returns>The member name to write.</returns>
    private static string DispatchKindMemberName(DispatchSiteKind kind)
        => kind switch
        {
            DispatchSiteKind.Command => "Command",
            DispatchSiteKind.Query => "Query",
            DispatchSiteKind.Stream => "Stream",
            DispatchSiteKind.Event => "Event",
            _ => "Message",
        };

    /// <summary>
    /// Selects the types a predicate accepts.
    /// </summary>
    /// <param name="types">The types to filter.</param>
    /// <param name="predicate">The test each type has to pass.</param>
    /// <returns>The accepted types, in order.</returns>
    private static List<RegistrableTypeModel> Filter(
        IReadOnlyList<RegistrableTypeModel> types,
        Func<RegistrableTypeModel, bool> predicate)
    {
        var filtered = new List<RegistrableTypeModel>(types.Count);

        foreach (var type in types)
        {
            if (predicate(type))
            {
                filtered.Add(type);
            }
        }

        return filtered;
    }

    /// <summary>
    /// Reports whether any of these types is a pipeline participant rather than a plain
    /// message.
    /// </summary>
    /// <param name="types">The types to test.</param>
    /// <returns><c>true</c> when one of them carries a handler contract.</returns>
    private static bool HasParticipants(IReadOnlyList<RegistrableTypeModel> types)
    {
        foreach (var type in types)
        {
            if (type.Descriptors.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports whether any of these types can be dispatched.
    /// </summary>
    /// <param name="types">The types to test.</param>
    /// <returns><c>true</c> when one of them is a dispatchable message.</returns>
    private static bool HasDispatchableMessages(IReadOnlyList<RegistrableTypeModel> types)
    {
        foreach (var type in types)
        {
            if (type.IsDispatchableMessage)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the catalog surface: the two <c>RegisterAll</c> overloads.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="wroteMember">Tracks whether a blank line is owed before the next member.</param>
    /// <param name="types">The types to select.</param>
    /// <param name="emitDispatchRoots">Whether this assembly roots dispatch generics.</param>
    private static void EmitCatalogSurface(
        StringBuilder sb,
        ref bool wroteMember,
        IReadOnlyList<RegistrableTypeModel> types,
        bool emitDispatchRoots)
    {
        StartMember(sb, ref wroteMember);
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        ///     Selects every discovered construct that participates in default discovery");
        sb.AppendLine("        ///     (no <c>[DiscoveryKey]</c>) into the given catalog, regardless of module.");
        sb.AppendLine("        /// </summary>");
        sb.Append("        public static void RegisterAll(").Append(CatalogFullName).AppendLine(" compositions)");
        sb.AppendLine("        {");
        sb.AppendLine("            RegisterAll(compositions, \"\");");
        sb.AppendLine("        }");

        StartMember(sb, ref wroteMember);
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        ///     Selects every discovered construct whose discovery keys match the given");
        sb.AppendLine("        ///     pattern — an exact key or a trailing-<c>*</c> prefix glob — regardless of");
        sb.AppendLine("        ///     module. Chained calls with overlapping patterns are safe: selection is a");
        sb.AppendLine("        ///     union.");
        sb.AppendLine("        /// </summary>");
        sb.Append("        public static void RegisterAll(").Append(CatalogFullName).AppendLine(" compositions, string discoveryKeyPattern)");
        sb.AppendLine("        {");

        if (emitDispatchRoots)
        {
            sb.AppendLine("            RootDispatchInstantiations();");
            sb.AppendLine();
        }

        EmitKeyedBody(sb, types, batchParticipants: false, receiver: "compositions", registerMethod: "Select");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Writes one module's registration surface: the two <c>RegisterGenerated</c> overloads.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="wroteMember">Tracks whether a blank line is owed before the next member.</param>
    /// <param name="builderFullName">The module builder the extensions hang off.</param>
    /// <param name="moduleName">The module's name, for the generated documentation.</param>
    /// <param name="moduleTypes">The types belonging to this module.</param>
    /// <param name="batchParticipants">
    /// Whether the referenced package offers the batch registration call.
    /// </param>
    /// <param name="emitDispatchRoots">Whether this assembly roots dispatch generics.</param>
    private static void EmitBuilderSurface(
        StringBuilder sb,
        ref bool wroteMember,
        string builderFullName,
        string moduleName,
        List<RegistrableTypeModel> moduleTypes,
        bool batchParticipants,
        bool emitDispatchRoots)
    {
        StartMember(sb, ref wroteMember);
        sb.AppendLine("        /// <summary>");
        sb.Append("        ///     Registers every discovered ").Append(moduleName).AppendLine("-module construct that participates");
        sb.AppendLine("        ///     in default discovery (no <c>[DiscoveryKey]</c>) — the bulk collection path.");
        sb.AppendLine("        /// </summary>");
        sb.Append("        public static ").Append(builderFullName).AppendLine(" RegisterGenerated(");
        sb.Append("            this ").Append(builderFullName).AppendLine(" builder)");
        sb.AppendLine("        {");
        sb.AppendLine("            return RegisterGenerated(builder, \"\");");
        sb.AppendLine("        }");

        StartMember(sb, ref wroteMember);
        sb.AppendLine("        /// <summary>");
        sb.Append("        ///     Registers every discovered ").Append(moduleName).AppendLine("-module construct whose discovery");
        sb.AppendLine("        ///     keys match the given pattern — an exact key or a trailing-<c>*</c> prefix");
        sb.AppendLine("        ///     glob. Chain calls to compose selections; overlapping patterns are safe.");
        sb.AppendLine("        /// </summary>");
        sb.Append("        public static ").Append(builderFullName).AppendLine(" RegisterGenerated(");
        sb.Append("            this ").Append(builderFullName).AppendLine(" builder, string discoveryKeyPattern)");
        sb.AppendLine("        {");

        if (emitDispatchRoots)
        {
            sb.AppendLine("            RootDispatchInstantiations();");
            sb.AppendLine();
        }

        EmitKeyedBody(sb, moduleTypes, batchParticipants, receiver: "builder", registerMethod: "Register");
        sb.AppendLine("            return builder;");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Writes the dispatch-root method: the roots and the compiled plans.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="wroteMember">Tracks whether a blank line is owed before the next member.</param>
    /// <param name="types">The discovered types.</param>
    /// <param name="registeredShadows">The hidden messages to root.</param>
    /// <param name="voidPlans">The single-handler plans for void pipelines.</param>
    /// <param name="resultPlans">The single-handler plans for result pipelines.</param>
    /// <param name="stagedPlans">The compiled pipeline bodies.</param>
    /// <param name="emitPlanFactories">
    /// Whether the referenced package accepts a construction factory with a plan.
    /// </param>
    /// <param name="emitProviderPlanFactories">
    /// Whether it accepts a provider-taking one.
    /// </param>
    /// <param name="hasKeyedServiceExtensions">
    /// Whether the consuming compilation can resolve the keyed-service extensions.
    /// </param>
    /// <param name="defaultResultAdapter">The container's fallback result adapter, if it names one.</param>
    /// <remarks>
    /// One <c>AddMessage</c> per dispatchable message, plus an <c>AddResult</c> or
    /// <c>AddStream</c> per closed result contract. Those closures let the runtime's dispatch
    /// caches build their executors and invokers without <c>MakeGenericType</c>, and give
    /// NativeAOT a static anchor for every instantiation. Every addition is idempotent, so
    /// each registration surface calls this without checking.
    /// </remarks>
    private static void EmitDispatchRoots(
        StringBuilder sb,
        ref bool wroteMember,
        IReadOnlyList<RegistrableTypeModel> types,
        IReadOnlyList<RegistrableTypeModel> registeredShadows,
        IReadOnlyList<VoidPlanModel> voidPlans,
        IReadOnlyList<ResultPlanModel> resultPlans,
        IReadOnlyList<StagedPlanModel> stagedPlans,
        bool emitPlanFactories,
        bool emitProviderPlanFactories,
        bool hasKeyedServiceExtensions,
        DefaultResultAdapterSiteModel? defaultResultAdapter)
    {
        StartMember(sb, ref wroteMember);
        // A module initializer rather than something a registration call does: the roots and
        // the compiled plans are properties of the compilation, like the frozen compositions,
        // so they enter the process-wide tables the moment the assembly loads. A container
        // that never calls RegisterGenerated or RegisterAll still dispatches through the
        // plans compiled here, and which rows it runs is settled by what it registered. The
        // registration surfaces call this too; every addition below is idempotent.
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void RootDispatchInstantiations()");
        sb.AppendLine("        {");

        foreach (var type in types)
        {
            // A derived event message implements no marker, so it cannot be named where
            // IMessage is required — and it needs no root: a publish is generic over the
            // event, so its dispatch closes without ever consulting the root table.
            if (!type.IsDispatchableMessage || !type.ImplementsMessageMarker)
            {
                continue;
            }

            AppendMessageRoot(sb, type);
        }

        // The hidden messages. Rooted, not registered: a root only lets a dispatch close its
        // generic without MakeGenericType, and which rows run is still whatever the container
        // selected.
        foreach (var shadow in registeredShadows)
        {
            AppendMessageRoot(sb, shadow);
        }

        AppendResultAdapterTable(sb, types, registeredShadows, defaultResultAdapter);

        // The single-handler plans. The executor checks each one against the pipeline the
        // container actually composed, so a plan can only lose its speedup, never change
        // behavior. A qualifying handler also carries a construction factory — a
        // parameterless `new`, or a provider-taking one for an injected constructor — which
        // the runtime uses only after confirming the handler's registration is the module's
        // own plain transient one.
        foreach (var plan in voidPlans)
        {
            sb.Append("            ").Append(DispatchRootsFullName)
              .Append(".AddVoidPlan<").Append(plan.MessageTypeExpression)
              .Append(", ").Append(plan.HandlerTypeExpression)
              .Append(">(");

            AppendPlanFactory(sb, plan.HandlerTypeExpression, plan.HasDirectConstruction,
                plan.ProviderConstructionExpression, plan.UsesKeyedServices,
                emitPlanFactories, emitProviderPlanFactories, hasKeyedServiceExtensions);

            sb.AppendLine(");");
        }

        foreach (var plan in resultPlans)
        {
            sb.Append("            ").Append(DispatchRootsFullName)
              .Append(".AddResultPlan<").Append(plan.MessageTypeExpression)
              .Append(", ").Append(plan.ResultTypeExpression)
              .Append(", ").Append(plan.HandlerTypeExpression)
              .Append(">(");

            AppendPlanFactory(sb, plan.HandlerTypeExpression, plan.HasDirectConstruction,
                plan.ProviderConstructionExpression, plan.UsesKeyedServices,
                emitPlanFactories, emitProviderPlanFactories, hasKeyedServiceExtensions);

            sb.AppendLine(");");
        }

        // The staged plans: straight-line pipeline bodies for messages with interceptors,
        // written as sealed classes below. Advisory like every plan — the executor compares
        // the composition they were baked against with the live one before running them.
        for (var i = 0; i < stagedPlans.Count; i++)
        {
            var plan = stagedPlans[i];

            // A broadcast goes into its own store: a publish looks there while a send looks
            // at the resultless plans, so which store answered settles the delivery
            // difference and no dispatch has to branch on the message.
            var addMethod = plan.IsGroupFiltering
                ? plan.IsBroadcast ? ".AddFilteredBroadcastPlan<" : ".AddFilteredPlan<"
                : plan.IsBroadcast ? ".AddBroadcastPlan<" : ".AddStagedPlan<";

            sb.Append("            ").Append(DispatchRootsFullName)
              .Append(addMethod)
              .Append(plan.MessageTypeExpression);

            if (plan.ResultTypeExpression is not null)
            {
                sb.Append(", ").Append(plan.ResultTypeExpression);
            }

            sb.Append(">(new StagedPlan").Append(i).Append("()");

            // The group set is part of the key, so it travels with the plan; the default set
            // is the argument left off, which is the overload without it. A filtering plan is
            // not keyed by a set at all — it carries the groups it covers instead.
            if (!plan.Groups.IsEmpty && !plan.IsGroupFiltering)
            {
                sb.Append(", new string[] { ");

                for (var g = 0; g < plan.Groups.Length; g++)
                {
                    sb.Append(g == 0 ? string.Empty : ", ")
                      .Append(SymbolDisplay.FormatLiteral(plan.Groups[g], quote: true));
                }

                sb.Append(" }");
            }

            sb.AppendLine(");");
        }

        sb.AppendLine("        }");
    }

    /// <summary>
    /// Writes the module initializer that adds this assembly's compositions to the
    /// process-wide table.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="wroteMember">Tracks whether a blank line is owed before the next member.</param>
    /// <param name="frozenCompositions">The compositions to write.</param>
    /// <remarks>
    /// A composition belongs to the compilation rather than to any registration call: a
    /// container that never calls <c>RegisterGenerated</c> still dispatches the messages
    /// compiled here, and a plugin assembly contributes its table the moment it loads. Which
    /// rows a given container runs is settled separately, by what that container registered.
    /// </remarks>
    private static void EmitFrozenCompositions(
        StringBuilder sb, ref bool wroteMember, IReadOnlyList<FrozenCompositionModel> frozenCompositions)
    {
        if (frozenCompositions.Count == 0)
        {
            return;
        }

        StartMember(sb, ref wroteMember);
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void PopulateFrozenCompositions()");
        sb.AppendLine("        {");

        foreach (var composition in frozenCompositions)
        {
            sb.Append("            ").Append(DispatchRootsFullName)
              .Append(".AddFrozenComposition(new ").Append(FrozenCompositionFullName).AppendLine("(");
            sb.Append("                typeof(").Append(composition.MessageTypeExpression).AppendLine("),");
            AppendFrozenSegment(sb, composition.Handlers);
            AppendFrozenSegment(sb, composition.IndirectHandlers);
            AppendFrozenSegment(sb, composition.PreInterceptors);
            AppendFrozenSegment(sb, composition.IndirectPreInterceptors);
            AppendFrozenSegment(sb, composition.PostInterceptors);
            AppendFrozenSegment(sb, composition.IndirectPostInterceptors);
            AppendFrozenSegment(sb, composition.ExceptionInterceptors);
            AppendFrozenSegment(sb, composition.IndirectExceptionInterceptors);
            AppendFrozenSegment(sb, composition.FinalInterceptors);
            AppendFrozenSegment(sb, composition.IndirectFinalInterceptors, last: true);
            sb.AppendLine("            ));");
        }

        sb.AppendLine("        }");
    }

    private const string FrozenCompositionFullName = "global::Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenComposition";
    private const string FrozenParticipantFullName = "global::Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenParticipant";

    /// <summary>
    /// Writes one segment of a composition.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="segment">The participants in that segment.</param>
    /// <param name="last">Whether this is the last segment, and so takes no trailing comma.</param>
    private static void AppendFrozenSegment(
        StringBuilder sb, ImmutableArray<FrozenParticipantModel> segment, bool last = false)
    {
        if (segment.IsEmpty)
        {
            sb.Append("                global::System.Array.Empty<").Append(FrozenParticipantFullName).Append(">()");
        }
        else
        {
            sb.Append("                new ").Append(FrozenParticipantFullName).Append("[] { ");

            for (var i = 0; i < segment.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append("new ").Append(FrozenParticipantFullName)
                  .Append("(typeof(").Append(segment[i].TypeofExpression).Append(')');

                if (segment[i].GroupsExpression is { } groupsExpression)
                {
                    sb.Append(", ").Append(groupsExpression);
                }

                sb.Append(')');
            }

            sb.Append(" }");
        }

        sb.AppendLine(last ? string.Empty : ",");
    }

    private const string HandlersNamespace = "global::Stella.Ergosfare.Core.Abstractions.Handlers.";
    private const string StagedCompositionFullName = "global::Stella.Ergosfare.Core.Abstractions.StagedPlans.StagedPlanKey";
    private const string ExecutionContextFullName = "global::Stella.Ergosfare.Core.Abstractions.ErgosfareContext";
    private const string AbortedExceptionFullName = "global::Stella.Ergosfare.Core.Abstractions.Exceptions.ExecutionAbortedException";
    private const string ValueTaskFullName = "global::System.Threading.Tasks.ValueTask";

    /// <summary>
    /// What a pipeline producing no result carries in its result slot.
    /// </summary>
    /// <remarks>
    /// Not the same thing as <see cref="ValueTaskFullName"/>, which stays the completion
    /// signal: a void plan still returns a <c>ValueTask</c>, it just carries
    /// <c>Unit.Value</c>.
    /// </remarks>
    private const string UnitFullName = "global::Stella.Ergosfare.Core.Abstractions.Unit";
    private const string GetRequiredServiceFullName = "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService";

    /// <summary>
    /// Writes one sealed class per staged plan.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="wroteMember">Tracks whether a blank line is owed before the next member.</param>
    /// <param name="stagedPlans">The plans to write.</param>
    /// <param name="supportsDirectConstruction">
    /// Whether the referenced package's plan base declares the directly constructed entry
    /// point.
    /// </param>
    /// <remarks>
    /// Each class holds the composition it was baked against and an <c>Execute</c> that runs
    /// that pipeline exactly as the general path would: the pre stages in order, each able to
    /// rewrite the message; the handler; the post stages, each able to rewrite the result; the
    /// exception stages, skipped for an <c>ExecutionAbortedException</c> and swallowing the
    /// exception once one of them ran; and the final stages, which always run. The casts match
    /// what the invokers do, unboxing included where a result is a value type. Participants
    /// resolve from the dispatching provider.
    /// </remarks>
    private static void EmitStagedPlanClasses(
        StringBuilder sb,
        ref bool wroteMember,
        IReadOnlyList<StagedPlanModel> stagedPlans,
        bool supportsDirectConstruction)
    {
        for (var i = 0; i < stagedPlans.Count; i++)
        {
            var plan = stagedPlans[i];
            var isVoid = plan.ResultTypeExpression is null;
            var emitDirect = supportsDirectConstruction && plan.SupportsDirectConstruction;

            StartMember(sb, ref wroteMember);
            sb.Append("        private sealed class StagedPlan").Append(i).Append(" : global::Stella.Ergosfare.Core.Abstractions.StagedPlans.");

            if (plan.IsBroadcast)
            {
                sb.Append("StagedBroadcastPlan<").Append(plan.MessageTypeExpression).AppendLine(">");
            }
            else if (isVoid)
            {
                sb.Append("StagedVoidPlan<").Append(plan.MessageTypeExpression).AppendLine(">");
            }
            else
            {
                sb.Append("StagedResultPlan<").Append(plan.MessageTypeExpression)
                  .Append(", ").Append(plan.ResultTypeExpression).AppendLine(">");
            }

            sb.AppendLine("        {");

            sb.Append("            private static readonly ").Append(StagedCompositionFullName)
              .Append(" BakedComposition = new ").Append(StagedCompositionFullName).AppendLine("(");
            AppendHandlerSegment(sb, plan.Handlers);
            sb.AppendLine(",");
            AppendHandlerSegment(sb, plan.IndirectHandlers);
            sb.AppendLine(",");
            AppendCompositionStage(sb, plan.PreCalls);
            sb.AppendLine(",");
            AppendCompositionStage(sb, plan.PostCalls);
            sb.AppendLine(",");
            AppendCompositionStage(sb, plan.ExceptionCalls);
            sb.AppendLine(",");
            AppendCompositionStage(sb, plan.FinalCalls);

            if (plan.AdapterKind != StagedResultAdapterKind.None)
            {
                // The adapter this plan was baked for: the executor trusts the plan only
                // while the slot's bound adapter is exactly this type.
                sb.AppendLine(",");
                sb.Append("                typeof(").Append(plan.ResultAdapterTypeExpression).Append(')');
            }

            sb.AppendLine(");");
            sb.AppendLine();

            if (plan.AdapterKind == StagedResultAdapterKind.Custom)
            {
                sb.Append("            private static readonly ").Append(plan.ResultAdapterTypeExpression)
                  .Append(" ResultAdapter = new ").Append(plan.ResultAdapterTypeExpression).AppendLine("();");
                sb.AppendLine();
            }
            sb.Append("            public override ").Append(StagedCompositionFullName).AppendLine(" Composition");
            sb.AppendLine("            {");
            sb.AppendLine("                get { return BakedComposition; }");
            sb.AppendLine("            }");
            sb.AppendLine();

            if (plan.IsGroupFiltering)
            {
                // The groups this body can be asked about. The plan is checked against the
                // composition over exactly these, that being the one set which reproduces
                // the participants the body carries.
                sb.Append("            private static readonly string[] CoveredGroups = new string[] { ");

                for (var g = 0; g < plan.Groups.Length; g++)
                {
                    sb.Append(g == 0 ? string.Empty : ", ")
                      .Append(SymbolDisplay.FormatLiteral(plan.Groups[g], quote: true));
                }

                sb.AppendLine(" };");
                sb.AppendLine();
                sb.AppendLine("            public override string[] FilterGroups");
                sb.AppendLine("            {");
                sb.AppendLine("                get { return CoveredGroups; }");
                sb.AppendLine("            }");
                sb.AppendLine();
            }

            sb.Append("            public override async ").Append(ValueTaskFullName);

            if (!isVoid)
            {
                sb.Append('<').Append(plan.ResultTypeExpression).Append('>');
            }

            sb.Append(plan.IsGroupFiltering ? " ExecuteFiltered(" : " Execute(").AppendLine();
            sb.Append("                ").Append(plan.MessageTypeExpression).AppendLine(" message,");
            sb.Append("                ").Append(ExecutionContextFullName).AppendLine(" context,");
            sb.Append("                global::System.IServiceProvider serviceProvider");
            AppendGroupsParameter(sb, plan);
            sb.AppendLine("            {");

            if (isVoid)
            {
                EmitVoidExecuteBody(sb, plan, direct: false);
            }
            else
            {
                EmitResultExecuteBody(sb, plan, direct: false);
            }

            sb.AppendLine("            }");

            // The unfiltered entry of a filtering plan. A dispatch naming no group asks for
            // the default one, which is what an empty set means to every test, so the same
            // body answers it with no participant treated specially.
            AppendUnfilteredForward(sb, plan, isVoid, direct: false);

            // The directly constructed variant: the same pipeline with every participant
            // built by `new`, which the executor uses only after confirming each one's
            // registration is the plain transient it expects.
            if (emitDirect)
            {
                sb.AppendLine();
                sb.AppendLine("            public override bool SupportsDirectConstruction");
                sb.AppendLine("            {");
                sb.AppendLine("                get { return true; }");
                sb.AppendLine("            }");
                sb.AppendLine();
                sb.Append("            public override async ").Append(ValueTaskFullName);

                if (!isVoid)
                {
                    sb.Append('<').Append(plan.ResultTypeExpression).Append('>');
                }

                sb.Append(plan.IsGroupFiltering ? " ExecuteFilteredDirect(" : " ExecuteDirect(").AppendLine();
                sb.Append("                ").Append(plan.MessageTypeExpression).AppendLine(" message,");
                sb.Append("                ").Append(ExecutionContextFullName).AppendLine(" context,");
                sb.Append("                global::System.IServiceProvider serviceProvider");
                AppendGroupsParameter(sb, plan);
                sb.AppendLine("            {");

                if (isVoid)
                {
                    EmitVoidExecuteBody(sb, plan, direct: true);
                }
                else
                {
                    EmitResultExecuteBody(sb, plan, direct: true);
                }

                sb.AppendLine("            }");

                AppendUnfilteredForward(sb, plan, isVoid, direct: true);
            }

            sb.AppendLine("        }");
        }
    }

    /// <summary>
    /// Writes one baked handler segment.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="handlers">The handlers in that segment.</param>
    /// <remarks>
    /// A single-handler plan writes a one-element array and an empty second segment, so the
    /// runtime compares it exactly as it compares a broadcast's — one shape, one check.
    /// </remarks>
    private static void AppendHandlerSegment(
        StringBuilder sb, ImmutableArray<StagedHandlerModel> handlers)
    {
        if (handlers.IsEmpty)
        {
            sb.Append("                global::System.Array.Empty<global::System.Type>()");
            return;
        }

        sb.Append("                new global::System.Type[] { ");

        for (var i = 0; i < handlers.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append("typeof(").Append(handlers[i].TypeExpression).Append(')');
        }

        sb.Append(" }");
    }

    /// <summary>
    /// Writes one baked interceptor stage.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="calls">The calls in that stage.</param>
    private static void AppendCompositionStage(StringBuilder sb, ImmutableArray<StagedCallModel> calls)
    {
        if (calls.IsEmpty)
        {
            sb.Append("                global::System.Array.Empty<global::System.Type>()");
            return;
        }

        sb.Append("                new global::System.Type[] { ");

        for (var i = 0; i < calls.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append("typeof(").Append(calls[i].TypeExpression).Append(')');
        }

        sb.Append(" }");
    }

    /// <summary>
    /// Writes the expression that resolves a type from the dispatching provider.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="typeExpression">The type to resolve.</param>
    private static void AppendResolve(StringBuilder sb, string typeExpression)
        => sb.Append(GetRequiredServiceFullName).Append('<').Append(typeExpression).Append(">(serviceProvider)");

    /// <summary>
    /// Writes the expression that obtains a participant.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="typeExpression">The participant's type.</param>
    /// <param name="constructionExpression">
    /// Its <c>new</c> expression in the directly constructed body, or <c>null</c> to resolve
    /// it from the container.
    /// </param>
    private static void AppendParticipant(StringBuilder sb, string typeExpression, string? constructionExpression)
    {
        if (constructionExpression is null)
        {
            AppendResolve(sb, typeExpression);
        }
        else
        {
            sb.Append(constructionExpression);
        }
    }

    /// <summary>
    /// Writes the cast of an object-typed chain value back to the pipeline's result type.
    /// </summary>
    /// <param name="resultExpression">The pipeline's result type.</param>
    /// <param name="resultIsValueType">
    /// Whether that result is a value type, which carries no null state and so takes the
    /// suppressing form.
    /// </param>
    /// <param name="targetAcceptsNull">
    /// Whether the stage being called declares its result parameter as <c>TResult?</c>.
    /// </param>
    /// <param name="operand">The chain variable being cast.</param>
    /// <returns>The cast expression, matching what the invokers do.</returns>
    /// <remarks>
    /// An exception or final interceptor declares its result parameter nullable; a post
    /// interceptor declares it plain. The difference matters for a reference-typed result: a
    /// cast to <c>TResult?</c> resets the expression to maybe-null however non-null the chain
    /// variable is, so handing one to a post interceptor is CS8604 in the consumer's build,
    /// and an outright failure where warnings are errors.
    /// </remarks>
    private static string ResultCast(string resultExpression, bool resultIsValueType, bool targetAcceptsNull, string operand)
        => resultIsValueType
            ? "(" + resultExpression + ")" + operand + "!"
            : targetAcceptsNull
                ? "(" + resultExpression + "?)" + operand
                : "(" + resultExpression + ")" + operand;

    /// <summary>
    /// Writes the plugin calls declared at one boundary of a plan.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="hook">The boundary these calls sit at.</param>
    /// <param name="indent">The indent to write them at.</param>
    /// <remarks>
    /// A <c>void</c> method is called plainly and never enters the state machine; a
    /// <c>ValueTask</c> one is awaited. Each call is closed over the plan's own message type,
    /// so a value-typed message crosses it unboxed, and the method's other parameters are
    /// filled from what the boundary has.
    /// </remarks>
    private static void EmitPluginCalls(
        StringBuilder sb, StagedPlanModel plan, PluginHook hook, string indent)
    {
        foreach (var call in plan.PluginCalls)
        {
            if (call.Hook != hook)
            {
                continue;
            }

            sb.Append(indent);

            if (call.IsAsync)
            {
                sb.Append("await ");
            }

            if (call.IsStatic)
            {
                sb.Append(call.ServiceTypeExpression);
            }
            else
            {
                // The plugin service comes from the dispatching provider like every other
                // participant, so a container's own registration still decides what runs.
                AppendResolve(sb, call.ServiceTypeExpression);
            }

            // Closed over the message alone: no hook carries a result, so the plan's result
            // type never reaches a hook's signature, and a void pipeline calls the method
            // exactly as a result-producing one does.
            sb.Append('.').Append(call.MethodName)
              .Append('<').Append(plan.MessageTypeExpression).Append(">(");

            for (var i = 0; i < call.Parameters.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                switch (call.Parameters[i].Kind)
                {
                    case PluginParameterKind.Message:
                        sb.Append("message");
                        break;
                    case PluginParameterKind.Context:
                        sb.Append("context");
                        break;
                    default:
                        AppendResolve(sb, call.Parameters[i].TypeExpression!);
                        break;
                }
            }

            sb.AppendLine(");");
        }
    }

    /// <summary>
    /// Writes the plan's main-handler calls.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    /// <param name="indent">The indent to write them at.</param>
    /// <remarks>
    /// The directly registered segment, followed for a broadcast by the covariantly matched
    /// one, since a publish delivers to everyone. A single-handler plan carries its covariant
    /// segment for the composition check and calls none of it: the priority ladder gives the
    /// message to its direct handler outright, so those handlers are in the composition
    /// without being part of the delivery. The pre- and post-handler plugin boundaries are
    /// written per handler, because that is the seam they name, and a broadcast has one per
    /// delivery.
    /// </remarks>
    private static void EmitHandlerCalls(StringBuilder sb, StagedPlanModel plan, bool direct, string indent)
    {
        EmitHandlerSegment(sb, plan, plan.Handlers, direct, indent);

        if (plan.IsBroadcast)
        {
            EmitHandlerSegment(sb, plan, plan.IndirectHandlers, direct, indent);
        }
    }

    /// <summary>
    /// Writes one segment's handler calls, each with its plugin boundaries around it.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="handlers">The handlers in that segment.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    /// <param name="indent">The indent to write them at.</param>
    private static void EmitHandlerSegment(
        StringBuilder sb,
        StagedPlanModel plan,
        ImmutableArray<StagedHandlerModel> handlers,
        bool direct,
        string indent)
    {
        foreach (var handler in handlers)
        {
            var body = OpenGuard(sb, handler.GroupGuard, indent);

            EmitPluginCalls(sb, plan, PluginHook.PreMain, body);
            sb.Append(body).Append("await ");
            AppendParticipant(sb, handler.TypeExpression, direct ? handler.ConstructionExpression : null);
            sb.AppendLine(".HandleAsync(message, context);");
            EmitPluginCalls(sb, plan, PluginHook.PostMain, body);

            CloseGuard(sb, handler.GroupGuard, indent);
        }
    }

    /// <summary>
    /// Writes the pre-interceptor calls.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    /// <param name="indent">The indent to write them at.</param>
    /// <remarks>
    /// Each call reassigns the message, which is how a pre-interceptor rewrites it.
    /// </remarks>
    private static void EmitPreCalls(StringBuilder sb, StagedPlanModel plan, bool direct, string indent)
    {
        foreach (var call in plan.PreCalls)
        {
            var body = OpenGuard(sb, call.GroupGuard, indent);

            sb.Append(body).Append("message = (").Append(plan.MessageTypeExpression).Append(") ");

            if (call.Arm == StagedCallArm.Sync)
            {
                sb.Append("((").Append(HandlersNamespace).Append("IPreInterceptor<").Append(plan.MessageTypeExpression).Append(">)");
                AppendParticipant(sb, call.TypeExpression, direct ? call.ConstructionExpression : null);
                sb.AppendLine(").Handle(message, context);");
            }
            else
            {
                sb.Append("await ((").Append(HandlersNamespace).Append("IAsyncPreInterceptor<").Append(plan.MessageTypeExpression).Append(">)");
                AppendParticipant(sb, call.TypeExpression, direct ? call.ConstructionExpression : null);
                sb.AppendLine(").HandleAsync(message, context);");
            }

            CloseGuard(sb, call.GroupGuard, indent);
        }
    }

    /// <summary>
    /// Writes one interceptor call for a stage that carries the result along.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="call">The interceptor to call.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    /// <param name="stageInterface">The stage's contract name, without its <c>I</c> prefix.</param>
    /// <param name="chainVariable">The variable carrying the result from call to call.</param>
    /// <param name="exceptionArgument">
    /// The exception to pass, for the exception stage; <c>null</c> for every other stage.
    /// </param>
    /// <param name="indent">The indent to write it at.</param>
    /// <remarks>
    /// The chain variable is object-typed and reassigned by each call, exactly as the runtime
    /// invoker loop does it.
    /// </remarks>
    private static void EmitChainCall(
        StringBuilder sb,
        StagedPlanModel plan,
        StagedCallModel call,
        bool direct,
        string stageInterface,
        string chainVariable,
        string? exceptionArgument,
        string indent)
    {
        var pipelineResult = plan.ResultTypeExpression ?? UnitFullName;
        var pipelineResultIsValueType = plan.ResultTypeExpression is not null && plan.ResultIsValueType;
        var extraArgument = exceptionArgument is null ? string.Empty : ", " + exceptionArgument;

        // The exception argument also tells the stages apart: only the exception stage
        // carries one, and only it declares its result parameter nullable. The post stage
        // takes TResult and object, not TResult? and object?.
        var targetAcceptsNull = exceptionArgument is not null;
        var outerIndent = indent;

        indent = OpenGuard(sb, call.GroupGuard, indent);

        sb.Append(indent).Append(chainVariable).Append(" = ");

        switch (call.Arm)
        {
            case StagedCallArm.AsyncTyped:
                sb.Append("await ((").Append(HandlersNamespace).Append("IAsync").Append(stageInterface)
                  .Append('<').Append(plan.MessageTypeExpression).Append(", ").Append(pipelineResult).Append(">)");
                AppendParticipant(sb, call.TypeExpression, direct ? call.ConstructionExpression : null);
                sb.Append(").HandleAsync(message, ")
                  .Append(ResultCast(pipelineResult, pipelineResultIsValueType, targetAcceptsNull, chainVariable))
                  .Append(extraArgument).AppendLine(", context);");
                break;
            case StagedCallArm.AsyncAgnostic:
                sb.Append("await ((").Append(HandlersNamespace).Append("IAsync").Append(stageInterface)
                  .Append('<').Append(plan.MessageTypeExpression).Append(">)");
                AppendParticipant(sb, call.TypeExpression, direct ? call.ConstructionExpression : null);
                sb.Append(").HandleAsync(message, ").Append(chainVariable)
                  .Append(exceptionArgument is null ? "!" : string.Empty)
                  .Append(extraArgument).AppendLine(", context);");
                break;
            default:
                sb.Append("((").Append(HandlersNamespace).Append('I').Append(stageInterface)
                  .Append('<').Append(plan.MessageTypeExpression).Append(", ").Append(pipelineResult).Append(">)");
                AppendParticipant(sb, call.TypeExpression, direct ? call.ConstructionExpression : null);
                sb.Append(").Handle(message, ")
                  .Append(ResultCast(pipelineResult, pipelineResultIsValueType, targetAcceptsNull, chainVariable))
                  .Append(extraArgument).AppendLine(", context);");
                break;
        }

        CloseGuard(sb, call.GroupGuard, outerIndent);
    }

    /// <summary>
    /// Writes the final-interceptor calls.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    /// <param name="resultExpressionText">The expression holding the pipeline's result.</param>
    /// <param name="indent">The indent to write them at.</param>
    /// <remarks>
    /// These run from a <c>finally</c>, so they see the exception when there was one and
    /// take the result as nullable — the pipeline may never have produced one.
    /// </remarks>
    private static void EmitFinalCalls(StringBuilder sb, StagedPlanModel plan, bool direct, string resultExpressionText, string indent)
    {
        var pipelineResult = plan.ResultTypeExpression ?? UnitFullName;
        var pipelineResultIsValueType = plan.ResultTypeExpression is not null && plan.ResultIsValueType;

        // A final interceptor declares `TResult? result`: the stage runs from a finally, so
        // there may be no result to hand it.
        const bool targetAcceptsNull = true;

        foreach (var call in plan.FinalCalls)
        {
            var body = OpenGuard(sb, call.GroupGuard, indent);

            switch (call.Arm)
            {
                case StagedCallArm.AsyncTyped:
                    sb.Append(body).Append("await ((").Append(HandlersNamespace).Append("IAsyncFinalInterceptor<")
                      .Append(plan.MessageTypeExpression).Append(", ").Append(pipelineResult).Append(">)");
                    AppendParticipant(sb, call.TypeExpression, direct ? call.ConstructionExpression : null);
                    sb.Append(").HandleAsync(message, ")
                      .Append(ResultCast(pipelineResult, pipelineResultIsValueType, targetAcceptsNull, resultExpressionText))
                      .AppendLine(", exception, context);");
                    break;
                case StagedCallArm.AsyncAgnostic:
                    sb.Append(body).Append("await ((").Append(HandlersNamespace).Append("IAsyncFinalInterceptor<")
                      .Append(plan.MessageTypeExpression).Append(">)");
                    AppendParticipant(sb, call.TypeExpression, direct ? call.ConstructionExpression : null);
                    sb.Append(").HandleAsync(message, ").Append(resultExpressionText).AppendLine(", exception, context);");
                    break;
                default:
                    sb.Append(body).Append("((").Append(HandlersNamespace).Append("IFinalInterceptor<")
                      .Append(plan.MessageTypeExpression).Append(", ").Append(pipelineResult).Append(">)");
                    AppendParticipant(sb, call.TypeExpression, direct ? call.ConstructionExpression : null);
                    sb.Append(").Handle(message, ")
                      .Append(ResultCast(pipelineResult, pipelineResultIsValueType, targetAcceptsNull, resultExpressionText))
                      .AppendLine(", exception, context);");
                    break;
            }

            CloseGuard(sb, call.GroupGuard, indent);
        }
    }

    /// <summary>
    /// Reports whether the plan's exception stage is certain to run something.
    /// </summary>
    /// <param name="plan">The plan to test.</param>
    /// <returns>
    /// <c>true</c> when one of its exception interceptors declares no filter, and so accepts
    /// every exception.
    /// </returns>
    private static bool StageAlwaysMatches(StagedPlanModel plan)
    {
        foreach (var call in plan.ExceptionCalls)
        {
            if (call.ExceptionFilterExpression is null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the exception stage's calls, each filtered participant behind the <c>is</c>
    /// test its runtime filter would apply.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    /// <param name="chainVariable">The variable carrying the result from call to call.</param>
    /// <param name="exceptionVariable">The variable holding the caught exception.</param>
    /// <param name="trackMatched">
    /// Whether the caller needs to know afterwards that something ran.
    /// </param>
    /// <param name="indent">The indent to write them at.</param>
    /// <returns><c>true</c> when the stage is certain to run something.</returns>
    /// <remarks>
    /// The flag is written only when the caller asked for it and no unfiltered participant
    /// already makes a miss impossible.
    /// </remarks>
    private static bool EmitExceptionCallLoop(
        StringBuilder sb, StagedPlanModel plan, bool direct, string chainVariable, string exceptionVariable,
        bool trackMatched, string indent)
    {
        var alwaysMatches = StageAlwaysMatches(plan);
        var emitFlag = trackMatched && !alwaysMatches;

        if (emitFlag)
        {
            sb.Append(indent).AppendLine("var matchedExceptionInterceptor = false;");
        }

        foreach (var call in plan.ExceptionCalls)
        {
            if (call.ExceptionFilterExpression is null)
            {
                EmitChainCall(sb, plan, call, direct, "ExceptionInterceptor", chainVariable, exceptionVariable, indent);
                continue;
            }

            sb.Append(indent).Append("if (").Append(exceptionVariable).Append(" is ")
              .Append(call.ExceptionFilterExpression).AppendLine(")");
            sb.Append(indent).AppendLine("{");

            if (emitFlag)
            {
                sb.Append(indent).AppendLine("    matchedExceptionInterceptor = true;");
            }

            EmitChainCall(sb, plan, call, direct, "ExceptionInterceptor", chainVariable, exceptionVariable, indent + "    ");
            sb.Append(indent).AppendLine("}");
        }

        return alwaysMatches;
    }

    /// <summary>
    /// Writes the exception stage inside a <c>catch</c>.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    /// <param name="chainVariable">The variable carrying the result from call to call.</param>
    /// <param name="indent">The indent to write it at.</param>
    /// <remarks>
    /// The calls, followed by what the runtime does when none of them accepted the exception:
    /// a bare rethrow. That can happen only where every participant is filtered — a stage
    /// that ran nobody has handled nothing — and a single unfiltered participant rules it out,
    /// in which case no flag is written at all.
    /// </remarks>
    private static void EmitExceptionCalls(
        StringBuilder sb, StagedPlanModel plan, bool direct, string chainVariable, string indent)
    {
        if (EmitExceptionCallLoop(sb, plan, direct, chainVariable, "e", trackMatched: true, indent))
        {
            return;
        }

        sb.Append(indent).AppendLine("if (!matchedExceptionInterceptor)");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).AppendLine("    throw;");
        sb.Append(indent).AppendLine("}");
    }

    /// <summary>
    /// Writes the body of a plan whose pipeline produces no result.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    /// <remarks>
    /// The stages still carry a result along, holding <c>Unit</c>, so a post- or
    /// exception-interceptor written against the void pipeline sees what it expects.
    /// </remarks>
    private static void EmitVoidExecuteBody(StringBuilder sb, StagedPlanModel plan, bool direct)
    {
        EmitGroupGuards(sb, plan, "                ");

        var needsGuards = !plan.PostCalls.IsEmpty || !plan.ExceptionCalls.IsEmpty || !plan.FinalCalls.IsEmpty;

        if (!needsGuards)
        {
            // A pipeline with pre-interceptors only: no exception stage and no final stage
            // means nothing to skip and nothing to clean up, so there is no try at all and
            // exceptions and aborts alike travel straight out. Every plugin hook is a
            // straight-line point, so a plugin never moves a plan off this shape.
            EmitPluginCalls(sb, plan, PluginHook.Start, "                ");
            EmitPreCalls(sb, plan, direct, "                ");
            EmitHandlerCalls(sb, plan, direct, "                ");
            EmitPluginCalls(sb, plan, PluginHook.Finish, "                ");
            return;
        }

        // The abort flag and the finally reading it exist for the final stage alone, so a
        // plan without one carries neither.
        var hasFinalStage = !plan.FinalCalls.IsEmpty;

        sb.AppendLine("                object? result = null;");
        sb.AppendLine("                global::System.Exception? exception = null;");

        if (hasFinalStage)
        {
            sb.AppendLine("                var aborted = false;");
        }

        sb.AppendLine("                try");
        sb.AppendLine("                {");
        EmitPluginCalls(sb, plan, PluginHook.Start, "                    ");
        EmitPreCalls(sb, plan, direct, "                    ");
        EmitHandlerCalls(sb, plan, direct, "                    ");
        sb.Append("                    result = ").Append(UnitFullName).AppendLine(".Value;");

        if (!plan.PostCalls.IsEmpty)
        {
            foreach (var call in plan.PostCalls)
            {
                EmitChainCall(sb, plan, call, direct, "PostInterceptor", "result", null, "                    ");
            }

            // What the general path does after the post stage: a null result restores the
            // pipeline's own value, and anything that is not a Unit fails the closed
            // nullable cast just as it would there.
            sb.Append("                    var invokedPostResult = (").Append(UnitFullName).AppendLine("?) result;");
            sb.Append("                    result = invokedPostResult == null ? ").Append(UnitFullName).AppendLine(".Value : result;");
        }

        EmitPluginCalls(sb, plan, PluginHook.Finish, "                    ");
        sb.AppendLine("                }");
        // A participant stopped the pipeline: nothing else runs, neither the exception stage
        // nor the final one, and the signal travels on to the caller.
        sb.Append("                catch (").Append(AbortedExceptionFullName).AppendLine(")");
        sb.AppendLine("                {");

        if (hasFinalStage)
        {
            sb.AppendLine("                    aborted = true;");
        }

        sb.AppendLine("                    throw;");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (global::System.Exception e)");
        sb.AppendLine("                {");
        sb.AppendLine("                    exception = e;");

        if (plan.ExceptionCalls.IsEmpty)
        {
            sb.AppendLine("                    throw;");
        }
        else
        {
            sb.AppendLine("                    var resultBeforeExceptions = result;");

            EmitExceptionCalls(sb, plan, direct, "result", "                    ");

            sb.Append("                    var invokedExceptionResult = (").Append(UnitFullName).AppendLine("?) result;");
            sb.AppendLine("                    result = invokedExceptionResult == null ? resultBeforeExceptions : result;");
        }

        sb.AppendLine("                }");

        if (hasFinalStage)
        {
            sb.AppendLine("                finally");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (!aborted)");
            sb.AppendLine("                    {");
            EmitFinalCalls(sb, plan, direct, "result", "                        ");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
        }
    }

    /// <summary>
    /// Writes the body of a plan whose pipeline produces a result.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    private static void EmitResultExecuteBody(StringBuilder sb, StagedPlanModel plan, bool direct)
    {
        EmitGroupGuards(sb, plan, "                ");

        if (plan.AdapterKind != StagedResultAdapterKind.None)
        {
            EmitAdaptedResultExecuteBody(sb, plan, direct);
            return;
        }

        var resultExpression = plan.ResultTypeExpression!;
        var needsGuards = !plan.PostCalls.IsEmpty || !plan.ExceptionCalls.IsEmpty || !plan.FinalCalls.IsEmpty;
        // As in the void body: the abort flag and its finally belong to the final stage.
        var hasFinalStage = !plan.FinalCalls.IsEmpty;

        if (!needsGuards)
        {
            // Pre-interceptors only, as in the void body: with nothing to skip and nothing to
            // clean up there is no try, and exceptions and aborts travel straight out.
            EmitPluginCalls(sb, plan, PluginHook.Start, "                ");
            EmitPreCalls(sb, plan, direct, "                ");
            EmitPluginCalls(sb, plan, PluginHook.PreMain, "                ");

            if (!plan.PluginCalls.IsEmpty)
            {
                // Plugin calls come after the handler, so its result lands in a local rather
                // than going straight out.
                sb.Append("                ").Append(resultExpression).Append(" result = await ");
                AppendParticipant(sb, plan.HandlerTypeExpression, direct ? plan.HandlerConstructionExpression : null);
                sb.AppendLine(".HandleAsync(message, context);");
                EmitPluginCalls(sb, plan, PluginHook.PostMain, "                ");
                EmitPluginCalls(sb, plan, PluginHook.Finish, "                ");
                sb.AppendLine("                return result;");
                return;
            }

            sb.Append("                return await ");
            AppendParticipant(sb, plan.HandlerTypeExpression, direct ? plan.HandlerConstructionExpression : null);
            sb.AppendLine(".HandleAsync(message, context);");
            return;
        }

        sb.Append("                ").Append(resultExpression).AppendLine(" result = default!;");
        sb.AppendLine("                global::System.Exception? exception = null;");

        if (hasFinalStage)
        {
            sb.AppendLine("                var aborted = false;");
        }

        sb.AppendLine("                try");
        sb.AppendLine("                {");
        EmitPluginCalls(sb, plan, PluginHook.Start, "                    ");
        EmitPreCalls(sb, plan, direct, "                    ");
        EmitPluginCalls(sb, plan, PluginHook.PreMain, "                    ");
        sb.Append("                    result = await ");
        AppendParticipant(sb, plan.HandlerTypeExpression, direct ? plan.HandlerConstructionExpression : null);
        sb.AppendLine(".HandleAsync(message, context);");
        EmitPluginCalls(sb, plan, PluginHook.PostMain, "                    ");

        if (!plan.PostCalls.IsEmpty)
        {
            sb.AppendLine("                    object? postChain = result;");

            foreach (var call in plan.PostCalls)
            {
                EmitChainCall(sb, plan, call, direct, "PostInterceptor", "postChain", null, "                    ");
            }

            if (plan.ResultIsValueType)
            {
                // For a value-typed result the general path's cast is a plain unbox, where a
                // null post result throws — so there is no null branch to write.
                sb.Append("                    result = (").Append(resultExpression).AppendLine(") postChain!;");
            }
            else
            {
                sb.Append("                    var postResult = (").Append(resultExpression).AppendLine("?) postChain;");
                sb.AppendLine("                    result = postResult == null ? result : postResult;");
            }
        }

        EmitPluginCalls(sb, plan, PluginHook.Finish, "                    ");
        sb.AppendLine("                }");
        // A participant stopped the pipeline: nothing else runs, and the signal travels on.
        sb.Append("                catch (").Append(AbortedExceptionFullName).AppendLine(")");
        sb.AppendLine("                {");

        if (hasFinalStage)
        {
            sb.AppendLine("                    aborted = true;");
        }

        sb.AppendLine("                    throw;");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (global::System.Exception e)");
        sb.AppendLine("                {");
        sb.AppendLine("                    exception = e;");

        if (plan.ExceptionCalls.IsEmpty)
        {
            sb.AppendLine("                    throw;");
        }
        else
        {
            sb.AppendLine("                    object? exceptionChain = result;");

            EmitExceptionCalls(sb, plan, direct, "exceptionChain", "                    ");

            if (plan.ResultIsValueType)
            {
                sb.Append("                    result = (").Append(resultExpression).AppendLine(") exceptionChain!;");
            }
            else
            {
                sb.Append("                    var exceptionResult = (").Append(resultExpression).AppendLine("?) exceptionChain;");
                sb.AppendLine("                    result = exceptionResult == null ? result : exceptionResult;");
            }
        }

        sb.AppendLine("                }");

        if (hasFinalStage)
        {
            sb.AppendLine("                finally");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (!aborted)");
            sb.AppendLine("                    {");
            EmitFinalCalls(sb, plan, direct, "result", "                        ");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
        }

        sb.AppendLine();
        sb.AppendLine("                return result;");
    }

    private const string ExceptionDispatchInfoFullName = "global::System.Runtime.ExceptionServices.ExceptionDispatchInfo";

    /// <summary>
    /// Writes the expression that turns a thrown exception into a failed carrier.
    /// </summary>
    /// <param name="plan">The plan being written.</param>
    /// <param name="exceptionVariable">The variable holding the exception.</param>
    /// <returns>
    /// The carrier's own <c>Fail</c> for a native carrier, the baked adapter's
    /// <c>Materialize</c> otherwise.
    /// </returns>
    private static string MaterializeExpression(StagedPlanModel plan, string exceptionVariable)
        => plan.AdapterKind == StagedResultAdapterKind.Native
            ? plan.ResultTypeExpression + ".Fail(" + exceptionVariable + ")"
            : "ResultAdapter.Materialize(" + exceptionVariable + ")";

    /// <summary>
    /// Writes the check that asks whether the handler's result carries a failure.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="indent">The indent to write it at.</param>
    /// <remarks>
    /// A field read for a native carrier, the baked adapter's <c>TryGetException</c>
    /// otherwise. Only the failure is taken out of it — the result already is the carrier.
    /// </remarks>
    private static void EmitHandlerProbe(StringBuilder sb, StagedPlanModel plan, string indent)
    {
        if (plan.AdapterKind == StagedResultAdapterKind.Native)
        {
            sb.Append(indent).AppendLine("if (result.Exception is { } carriedException)");
        }
        else
        {
            sb.Append(indent).AppendLine(
                "if (ResultAdapter.TryGetException(in result, out var carriedException) && carriedException is not null)");
        }

        sb.Append(indent).AppendLine("{");
        sb.Append(indent).AppendLine("    exception = carriedException;");
        sb.Append(indent).AppendLine("}");
    }

    /// <summary>
    /// Writes the check that asks whether one post-interceptor's result carries a failure.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="probeIndex">The position of this check, which names its locals.</param>
    /// <param name="indent">The indent to write it at.</param>
    /// <remarks>
    /// A failed carrier the interceptor produced becomes the pipeline's result, and its
    /// failure moves the pipeline on — the same thing the runtime's post loop does.
    /// </remarks>
    private static void EmitPostProbe(StringBuilder sb, StagedPlanModel plan, int probeIndex, string indent)
    {
        var carrier = "postCarrier" + probeIndex;
        var failure = "postException" + probeIndex;

        sb.Append(indent).Append("if (postChain is ").Append(plan.ResultTypeExpression).Append(' ').Append(carrier);

        if (plan.AdapterKind == StagedResultAdapterKind.Native)
        {
            sb.Append(" && ").Append(carrier).Append(".Exception is { } ").Append(failure).AppendLine(")");
        }
        else
        {
            sb.Append(" && ResultAdapter.TryGetException(in ").Append(carrier).Append(", out var ").Append(failure)
              .Append(") && ").Append(failure).AppendLine(" is not null)");
        }

        sb.Append(indent).AppendLine("{");
        sb.Append(indent).Append("    result = ").Append(carrier).AppendLine(";");
        sb.Append(indent).Append("    exception = ").Append(failure).AppendLine(";");
        sb.Append(indent).AppendLine("}");
    }

    /// <summary>
    /// Writes the merge of an object-typed chain back into the result.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="chainVariable">The variable the stage carried its result in.</param>
    /// <param name="mergedVariable">The local the merged value lands in.</param>
    /// <param name="indent">The indent to write it at.</param>
    /// <remarks>
    /// The same merge the general path performs; see the unadapted bodies for why a
    /// value-typed result unboxes instead of taking a null branch.
    /// </remarks>
    private static void EmitChainMerge(
        StringBuilder sb, StagedPlanModel plan, string chainVariable, string mergedVariable, string indent)
    {
        var resultExpression = plan.ResultTypeExpression!;

        if (plan.ResultIsValueType)
        {
            sb.Append(indent).Append("result = (").Append(resultExpression).Append(") ").Append(chainVariable).AppendLine("!;");
        }
        else
        {
            sb.Append(indent).Append("var ").Append(mergedVariable).Append(" = (").Append(resultExpression)
              .Append("?) ").Append(chainVariable).AppendLine(";");
            sb.Append(indent).Append("result = ").Append(mergedVariable).Append(" == null ? result : ")
              .Append(mergedVariable).AppendLine(";");
        }
    }

    /// <summary>
    /// Writes the body of a result plan whose slot has a baked adapter.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="plan">The plan being written.</param>
    /// <param name="direct">Whether participants are constructed rather than resolved.</param>
    /// <remarks>
    /// The counterpart of <see cref="EmitResultExecuteBody"/> for a pipeline whose failures
    /// travel as values. A check after the handler and after each post-interceptor enters the
    /// exception stage as an ordinary branch, so nothing is thrown to get there, and the stage
    /// runs between the pipeline's own catch and the final stage. A real throw is caught and
    /// turned into a failed carrier where the carrier can hold one; where it cannot, the
    /// exception is captured and rethrown after the final stage.
    /// </remarks>
    private static void EmitAdaptedResultExecuteBody(StringBuilder sb, StagedPlanModel plan, bool direct)
    {
        var resultExpression = plan.ResultTypeExpression!;
        var materializes = plan.ResultAdapterMaterializes;
        var needsGuards = !plan.PostCalls.IsEmpty || !plan.ExceptionCalls.IsEmpty || !plan.FinalCalls.IsEmpty;

        if (!needsGuards)
        {
            // Pre-interceptors only, so there is no stage to inform: a carried failure either
            // travels out inside its carrier or surfaces as a throw, and a real throw becomes
            // a failed carrier where the carrier can hold one and travels on unchanged where
            // it cannot.
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            EmitPluginCalls(sb, plan, PluginHook.Start, "                    ");
            EmitPreCalls(sb, plan, direct, "                    ");
            EmitPluginCalls(sb, plan, PluginHook.PreMain, "                    ");

            if (materializes && plan.PluginCalls.IsEmpty)
            {
                sb.Append("                    return await ");
                AppendParticipant(sb, plan.HandlerTypeExpression, direct ? plan.HandlerConstructionExpression : null);
                sb.AppendLine(".HandleAsync(message, context);");
            }
            else if (materializes)
            {
                // Plugin calls stand between the handler and the return, so its result lands
                // in a local first.
                sb.Append("                    var handlerResult = await ");
                AppendParticipant(sb, plan.HandlerTypeExpression, direct ? plan.HandlerConstructionExpression : null);
                sb.AppendLine(".HandleAsync(message, context);");
                EmitPluginCalls(sb, plan, PluginHook.PostMain, "                    ");
                EmitPluginCalls(sb, plan, PluginHook.Finish, "                    ");
                sb.AppendLine("                    return handlerResult;");
            }
            else
            {
                sb.Append("                    var handlerResult = await ");
                AppendParticipant(sb, plan.HandlerTypeExpression, direct ? plan.HandlerConstructionExpression : null);
                sb.AppendLine(".HandleAsync(message, context);");
                EmitPluginCalls(sb, plan, PluginHook.PostMain, "                    ");
                sb.AppendLine();
                sb.AppendLine("                    if (ResultAdapter.TryGetException(in handlerResult, out var carriedException) && carriedException is not null)");
                sb.AppendLine("                    {");
                sb.AppendLine("                        throw carriedException;");
                sb.AppendLine("                    }");
                sb.AppendLine();
                EmitPluginCalls(sb, plan, PluginHook.Finish, "                    ");
                sb.AppendLine("                    return handlerResult;");
            }

            sb.AppendLine("                }");
            sb.Append("                catch (").Append(AbortedExceptionFullName).AppendLine(")");
            sb.AppendLine("                {");
            sb.AppendLine("                    return default!;");
            sb.AppendLine("                }");

            if (materializes)
            {
                sb.AppendLine("                catch (global::System.Exception e)");
                sb.AppendLine("                {");
                sb.Append("                    return ").Append(MaterializeExpression(plan, "e")).AppendLine(";");
                sb.AppendLine("                }");
            }

            return;
        }

        // A place to hold an unhandled failure until after the final stage is needed only
        // when the carrier cannot hold one and the exception stage might run nobody.
        var needsUnhandled = !materializes && !StageAlwaysMatches(plan);

        sb.Append("                ").Append(resultExpression).AppendLine(" result = default!;");
        sb.AppendLine("                global::System.Exception? exception = null;");

        if (needsUnhandled)
        {
            sb.Append("                ").Append(ExceptionDispatchInfoFullName).AppendLine("? unhandledException = null;");
        }

        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        EmitPluginCalls(sb, plan, PluginHook.Start, "                        ");
        EmitPreCalls(sb, plan, direct, "                        ");
        EmitPluginCalls(sb, plan, PluginHook.PreMain, "                        ");
        sb.Append("                        result = await ");
        AppendParticipant(sb, plan.HandlerTypeExpression, direct ? plan.HandlerConstructionExpression : null);
        sb.AppendLine(".HandleAsync(message, context);");
        sb.AppendLine();
        EmitHandlerProbe(sb, plan, "                        ");
        EmitPluginCalls(sb, plan, PluginHook.PostMain, "                        ");

        if (!plan.PostCalls.IsEmpty)
        {
            sb.AppendLine();
            sb.AppendLine("                        object? postChain = result;");

            var probeIndex = 0;

            foreach (var call in plan.PostCalls)
            {
                sb.AppendLine();
                sb.AppendLine("                        if (exception is null)");
                sb.AppendLine("                        {");
                EmitChainCall(sb, plan, call, direct, "PostInterceptor", "postChain", null, "                            ");
                EmitPostProbe(sb, plan, probeIndex++, "                            ");
                sb.AppendLine("                        }");
            }

            sb.AppendLine();
            sb.AppendLine("                        if (exception is null)");
            sb.AppendLine("                        {");
            EmitChainMerge(sb, plan, "postChain", "postResult", "                            ");
            sb.AppendLine("                        }");
        }

        if (plan.HasPluginCalls(PluginHook.Finish))
        {
            // The result may already carry a failure here; either way the calls see what the
            // stage settled on, which is what this boundary means.
            sb.AppendLine();
            EmitPluginCalls(sb, plan, PluginHook.Finish, "                        ");
        }

        sb.AppendLine("                    }");
        // A stop, not a failure; see the unadapted body.
        sb.Append("                    catch (").Append(AbortedExceptionFullName).AppendLine(")");
        sb.AppendLine("                    {");
        sb.AppendLine("                    }");
        sb.AppendLine("                    catch (global::System.Exception e)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        exception = e;");

        if (materializes)
        {
            sb.Append("                        result = ").Append(MaterializeExpression(plan, "e")).AppendLine(";");
        }

        sb.AppendLine("                    }");

        if (!plan.ExceptionCalls.IsEmpty)
        {
            sb.AppendLine();
            sb.AppendLine("                    if (exception is not null)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        object? exceptionChain = result;");
            EmitExceptionCallLoop(sb, plan, direct, "exceptionChain", "exception",
                trackMatched: needsUnhandled, "                        ");

            if (needsUnhandled)
            {
                sb.AppendLine("                        if (!matchedExceptionInterceptor)");
                sb.AppendLine("                        {");
                sb.Append("                            unhandledException = ").Append(ExceptionDispatchInfoFullName)
                  .AppendLine(".Capture(exception);");
                sb.AppendLine("                        }");
            }

            EmitChainMerge(sb, plan, "exceptionChain", "exceptionResult", "                        ");
            sb.AppendLine("                    }");
        }
        else if (needsUnhandled)
        {
            sb.AppendLine();
            sb.AppendLine("                    if (exception is not null)");
            sb.AppendLine("                    {");
            sb.Append("                        unhandledException = ").Append(ExceptionDispatchInfoFullName)
              .AppendLine(".Capture(exception);");
            sb.AppendLine("                    }");
        }

        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        EmitFinalCalls(sb, plan, direct, "result", "                    ");
        sb.AppendLine("                }");
        sb.AppendLine();

        if (needsUnhandled)
        {
            sb.AppendLine("                if (unhandledException is not null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    unhandledException.Throw();");
            sb.AppendLine("                }");
            sb.AppendLine();
        }

        sb.AppendLine("                return result;");
    }

    /// <summary>
    /// Writes the compilation's result-adapter table.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="types">The discovered types.</param>
    /// <param name="registeredShadows">The hidden messages a registration reaches.</param>
    /// <param name="defaultResultAdapter">The container's fallback adapter, if it names one.</param>
    /// <remarks>
    /// <para>
    /// One entry per (message, result) slot an annotation binds, one per message that opts
    /// out, and one per result type the fallback serves — the same three tiers the runtime
    /// binding consults, answered here where the types are still types. Each entry passes its
    /// adapter as a type argument constrained to the slot's contract, so an adapter that does
    /// not serve the slot, or cannot be constructed, is a compile error in this file rather
    /// than a reflective test at first dispatch.
    /// </para>
    /// <para>
    /// The seal closes it: past that call a slot missing from the table means the tier bound
    /// nothing, and a configured fallback in a process that never got a table says so instead
    /// of silently serving no one.
    /// </para>
    /// </remarks>
    private static void AppendResultAdapterTable(
        StringBuilder sb,
        IReadOnlyList<RegistrableTypeModel> types,
        IReadOnlyList<RegistrableTypeModel> registeredShadows,
        DefaultResultAdapterSiteModel? defaultResultAdapter)
    {
        var defaultSlots = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var type in types.Concat(registeredShadows))
        {
            if (!type.IsDispatchableMessage)
            {
                continue;
            }

            if (type.HasIgnoredResultAdapter)
            {
                // The opt-out outranks every tier, so the slots below are not written for it
                // at all — the one entry is the whole answer.
                sb.Append("            ").Append(DispatchRootsFullName)
                  .Append(".AddIgnoredResultAdapter<").Append(type.TypeofExpression).AppendLine(">();");
                continue;
            }

            foreach (var slot in ResultSlots(type))
            {
                if (type.ResultAdapter is { IsBakeable: true } annotation && annotation.Fits(slot))
                {
                    sb.Append("            ").Append(DispatchRootsFullName)
                      .Append(".AddResultAdapter<").Append(type.TypeofExpression)
                      .Append(", ").Append(slot)
                      .Append(", ").Append(annotation.TypeofExpression).AppendLine(">();");
                    continue;
                }

                // The fallback answers per result type rather than per message, so the slots
                // are collected and written once each.
                defaultSlots.Add(slot);
            }
        }

        if (defaultResultAdapter is { IsBakeable: true })
        {
            var binder = new DefaultResultAdapterBinder(defaultResultAdapter);

            foreach (var slot in defaultSlots)
            {
                if (binder.TryBind(slot, out var adapterTypeExpression, out _))
                {
                    sb.Append("            ").Append(DispatchRootsFullName)
                      .Append(".AddDefaultResultAdapter<").Append(slot)
                      .Append(", ").Append(adapterTypeExpression).AppendLine(">();");
                }
            }
        }

        sb.Append("            ").Append(DispatchRootsFullName).AppendLine(".SealResultAdapters();");
    }

    /// <summary>
    /// The result slots a message's dispatches bind an adapter for.
    /// </summary>
    /// <param name="type">The message to read.</param>
    /// <returns>Each slot's type expression.</returns>
    /// <remarks>
    /// A command's void dispatch binds over <c>Unit</c>, a stream's over its enumerator, and
    /// everything else over the declared result type — which is the set of slots the runtime
    /// asks the binding about.
    /// </remarks>
    private static IEnumerable<string> ResultSlots(RegistrableTypeModel type)
    {
        if (type.IsCommand)
        {
            yield return EmittedExpressions.Unit;
        }

        foreach (var result in type.DispatchResults)
        {
            yield return result.IsStream
                ? EmittedExpressions.AsyncEnumerator + "<" + result.ResultTypeExpression + ">"
                : result.ResultTypeExpression;
        }
    }

    /// <summary>
    /// Writes one message's dispatch roots.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="type">The message to root.</param>
    /// <remarks>
    /// The message generic first, then one root per result contract it declares. Both are
    /// closed at compile time, which is what keeps <c>MakeGenericType</c> off the dispatch
    /// path.
    /// </remarks>
    private static void AppendMessageRoot(StringBuilder sb, RegistrableTypeModel type)
    {
        sb.Append("            ").Append(DispatchRootsFullName)
          .Append(".AddMessage<").Append(type.TypeofExpression).AppendLine(">();");

        foreach (var result in type.DispatchResults)
        {
            sb.Append("            ").Append(DispatchRootsFullName)
              .Append(result.IsStream ? ".AddStream<" : ".AddResult<")
              .Append(type.TypeofExpression).Append(", ").Append(result.ResultTypeExpression)
              .AppendLine(">();");
        }
    }

    /// <summary>
    /// Writes a plan's construction-factory argument, when it has one.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="handlerTypeExpression">The handler the plan calls.</param>
    /// <param name="hasDirectConstruction">Whether a parameterless <c>new</c> would do.</param>
    /// <param name="providerConstructionExpression">
    /// The provider-taking factory, or <c>null</c> when the handler does not qualify for one.
    /// </param>
    /// <param name="usesKeyedServices">Whether that factory resolves a keyed service.</param>
    /// <param name="emitPlanFactories">
    /// Whether the referenced package accepts a factory with a plan.
    /// </param>
    /// <param name="emitProviderPlanFactories">Whether it accepts a provider-taking one.</param>
    /// <param name="hasKeyedServiceExtensions">
    /// Whether the consuming compilation can resolve the keyed-service extensions.
    /// </param>
    /// <remarks>
    /// The parameterless shape is preferred, being the cheaper one and available against
    /// older packages; the provider-taking one covers an injected constructor, and is written
    /// only when the consuming compilation can spell every resolution in it.
    /// </remarks>
    private static void AppendPlanFactory(
        StringBuilder sb,
        string handlerTypeExpression,
        bool hasDirectConstruction,
        string? providerConstructionExpression,
        bool usesKeyedServices,
        bool emitPlanFactories,
        bool emitProviderPlanFactories,
        bool hasKeyedServiceExtensions)
    {
        if (emitPlanFactories && hasDirectConstruction)
        {
            sb.Append("static () => new ").Append(handlerTypeExpression).Append("()");
        }
        else if (emitProviderPlanFactories
                 && providerConstructionExpression is not null
                 && (!usesKeyedServices || hasKeyedServiceExtensions))
        {
            sb.Append(providerConstructionExpression);
        }
    }

    /// <summary>
    /// Writes the registration body the catalog and builder surfaces share.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="types">The types to register.</param>
    /// <param name="batchParticipants">
    /// Whether participants are gathered into one batch call; off for the catalog surface and
    /// for a package predating the batch surface.
    /// </param>
    /// <param name="receiver">The identifier the registration calls are made on.</param>
    /// <param name="registerMethod">The per-type method to call.</param>
    /// <remarks>
    /// One block per discovery-key cluster, each behind a key-match test. A message is named
    /// one call at a time; participants go into a local list and are registered in a single
    /// batch at the end, or one at a time through the same per-type call when batching is off.
    /// </remarks>
    private static void EmitKeyedBody(
        StringBuilder sb,
        IReadOnlyList<RegistrableTypeModel> types,
        bool batchParticipants,
        string receiver,
        string registerMethod)
    {
        var emitParticipants = batchParticipants && HasParticipants(types);

        if (emitParticipants)
        {
            sb.AppendLine("            var participants = new global::System.Collections.Generic.List<global::System.Type>();");
            sb.AppendLine();
        }

        var clusters = BuildClusters(types);

        for (var i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];

            if (i > 0)
            {
                sb.AppendLine();
            }

            sb.Append("            if (");

            for (var k = 0; k < cluster.Keys.Count; k++)
            {
                if (k > 0)
                {
                    sb.Append(" || ");
                }

                sb.Append("MatchesDiscoveryKey(")
                  .Append(SymbolDisplay.FormatLiteral(cluster.Keys[k], quote: true))
                  .Append(", discoveryKeyPattern)");
            }

            sb.AppendLine(")");
            sb.AppendLine("            {");

            foreach (var type in cluster.Types)
            {
                if (emitParticipants && type.Descriptors.Length > 0)
                {
                    sb.Append("                participants.Add(typeof(")
                      .Append(type.TypeofExpression).AppendLine("));");
                }
                else
                {
                    sb.Append("                ").Append(receiver)
                      .Append('.').Append(registerMethod)
                      .Append("(typeof(").Append(type.TypeofExpression).AppendLine("));");
                }
            }

            sb.AppendLine("            }");
        }

        if (emitParticipants)
        {
            sb.AppendLine();
            sb.AppendLine("            if (participants.Count > 0)");
            sb.AppendLine("            {");
            sb.Append("                ").Append(receiver).AppendLine(".RegisterParticipants(participants);");
            sb.AppendLine("            }");
        }
    }

    /// <summary>
    /// Writes the helper the generated blocks test their discovery keys with.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="wroteMember">Tracks whether a blank line is owed before the next member.</param>
    private static void EmitMatchesHelper(StringBuilder sb, ref bool wroteMember)
    {
        StartMember(sb, ref wroteMember);
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        ///     Whether a discovery key matches a pattern: ordinal equality, or — when the");
        sb.AppendLine("        ///     pattern ends with <c>*</c> — an ordinal prefix match on the part before the");
        sb.AppendLine("        ///     star. Mirrors the runtime <c>Discovery</c> helper.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        private static bool MatchesDiscoveryKey(string key, string discoveryKeyPattern)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (discoveryKeyPattern.Length > 0 && discoveryKeyPattern[discoveryKeyPattern.Length - 1] == '*')");
        sb.AppendLine("            {");
        sb.AppendLine("                return key.Length >= discoveryKeyPattern.Length - 1");
        sb.AppendLine("                    && string.CompareOrdinal(key, 0, discoveryKeyPattern, 0, discoveryKeyPattern.Length - 1) == 0;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return string.Equals(key, discoveryKeyPattern, global::System.StringComparison.Ordinal);");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Groups the types by the discovery keys they share.
    /// </summary>
    /// <param name="types">The types to group.</param>
    /// <returns>The clusters, ordered so the same input always writes the same blocks.</returns>
    private static List<Cluster> BuildClusters(IReadOnlyList<RegistrableTypeModel> types)
    {
        var bySignature = new Dictionary<string, Cluster>(StringComparer.Ordinal);
        var clusters = new List<Cluster>();

        foreach (var type in types)
        {
            var keys = EffectiveKeys(type);
            var signature = string.Join("\u001f", keys);

            if (!bySignature.TryGetValue(signature, out var cluster))
            {
                cluster = new Cluster(signature, keys);
                bySignature.Add(signature, cluster);
                clusters.Add(cluster);
            }

            cluster.Types.Add(type);
        }

        // A fixed block order, with the default cluster's empty signature sorting first.
        clusters.Sort(static (x, y) => string.CompareOrdinal(x.Signature, y.Signature));

        return clusters;
    }

    /// <summary>
    /// Gives the discovery keys a type is written under.
    /// </summary>
    /// <param name="type">The type to read.</param>
    /// <returns>
    /// The implicit default key when it declares none, otherwise its own keys, sorted and
    /// deduplicated so two equivalent declarations land in one cluster.
    /// </returns>
    private static List<string> EffectiveKeys(in RegistrableTypeModel type)
    {
        if (type.DiscoveryKeys.IsEmpty)
        {
            return [""];
        }

        var keys = new List<string>(type.DiscoveryKeys.Length);

        foreach (var key in type.DiscoveryKeys)
        {
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        keys.Sort(static (x, y) => string.CompareOrdinal(x, y));

        return keys;
    }

    /// <summary>
    /// Separates one generated member from the last.
    /// </summary>
    /// <param name="sb">The buffer to write to.</param>
    /// <param name="wroteMember">
    /// Whether a member was already written; set on the way out.
    /// </param>
    private static void StartMember(StringBuilder sb, ref bool wroteMember)
    {
        if (wroteMember)
        {
            sb.AppendLine();
        }

        wroteMember = true;
    }
}
