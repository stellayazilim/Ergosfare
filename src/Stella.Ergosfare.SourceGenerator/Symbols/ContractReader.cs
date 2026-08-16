using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
///     Reads a type's Ergosfare contracts off its interface list: which pipeline stages it
///     participates in and for which messages (the descriptors), what shapes its handler
///     members take (the contract shapes), and — for a message — whether anything can be
///     dispatched with it and with what result.
/// </summary>

internal static class ContractReader
{
    /// <summary>
    ///     Whether the type can appear as a dispatched message instance: a concrete,
    ///     fully closed class or struct with no handler contracts. Only such types get
    ///     dispatch roots — abstract types and interfaces never carry a runtime message's
    ///     type, handlers are never dispatched, and open generics cannot be rooted.
    /// </summary>
    /// <summary>
    ///     Whether the type is a message shape a composition can be computed for: a
    ///     construct carrying no handler contracts of its own. Wider than
    ///     <see cref="IsDispatchableMessage"/> — an abstract base or an interface is never
    ///     dispatched itself, but it is what the frozen table's ancestor ladder lands on
    ///     when a message the generator never saw (a proxy, a type hidden from discovery)
    ///     is dispatched.
    /// </summary>
    /// <remarks>
    ///     A generic message definition is a shape too, and unlike
    ///     <see cref="IsDispatchableMessage"/> it needs no closed instantiation: the table
    ///     keys generic messages by their definition and the lookup normalizes a runtime
    ///     <c>Wrap&lt;int&gt;</c> to <c>Wrap&lt;&gt;</c>, so one entry serves every
    ///     instantiation. A generic containing type is still refused — see
    ///     <see cref="BuildDescriptors"/>.
    /// </remarks>
    internal static bool IsMessageShape(INamedTypeSymbol symbol, ImmutableArray<DescriptorModel> descriptors)
    {
        if (descriptors.Length > 0)
        {
            return false;
        }

        if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface))
        {
            return false;
        }

        for (var current = symbol.ContainingType; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsDispatchableMessage(INamedTypeSymbol symbol, ImmutableArray<DescriptorModel> descriptors)
    {
        if (symbol.IsAbstract || descriptors.Length > 0)
        {
            return false;
        }

        if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return false;
        }

        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Collects the result roots of a dispatchable message from its closed marker
    ///     contracts: <c>ICommand&lt;T&gt;</c>/<c>IQuery&lt;T&gt;</c> feed the
    ///     result-executor path, <c>IStreamQuery&lt;T&gt;</c> the streaming path.
    /// </summary>
    internal static ImmutableArray<DispatchResultModel> GetDispatchResults(INamedTypeSymbol symbol)
    {
        ImmutableArray<DispatchResultModel>.Builder? results = null;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Arity != 1)
            {
                continue;
            }

            var isStream = false;

            switch (iface.Name)
            {
                case "ICommand" when SymbolNaming.IsInNamespace(iface, ContractNames.CommandMarkerNamespace):
                case "IQuery" when SymbolNaming.IsInNamespace(iface, ContractNames.QueryMarkerNamespace):
                    break;
                case "IStreamQuery" when SymbolNaming.IsInNamespace(iface, ContractNames.QueryMarkerNamespace):
                    isStream = true;
                    break;
                default:
                    continue;
            }

            var model = new DispatchResultModel(
                SymbolNaming.VerbatimTypeExpression(iface.TypeArguments[0]), isStream, iface.TypeArguments[0].IsValueType);

            results ??= ImmutableArray.CreateBuilder<DispatchResultModel>();

            if (!results.Contains(model))
            {
                results.Add(model);
            }
        }

        return results?.ToImmutable() ?? ImmutableArray<DispatchResultModel>.Empty;
    }

    /// <summary>
    ///     Collects the raw interceptor contracts the type implements — the undeduped
    ///     counterpart of <see cref="BuildDescriptors"/>'s interceptor walk, keeping the
    ///     async/sync and result-typed facts the staged-plan arm selection needs.
    /// </summary>
    internal static ImmutableArray<ContractShapeModel> BuildContractShapes(INamedTypeSymbol symbol)
    {
        // Arity alone is not the question — a closed constructed participant has the same
        // arity as the definition it came from, and its contracts name concrete types. What
        // disqualifies a type here is an *unbound* level anywhere in its chain: the staged
        // plan has to name the participant, and a name with an open level cannot be written.
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0 && Monomorphizer.IsUnboundOrDefinition(current))
            {
                return ImmutableArray<ContractShapeModel>.Empty;
            }
        }

        ImmutableArray<ContractShapeModel>.Builder? shapes = null;

        ReadExceptionFilter(symbol, out var exceptionFilterExpression, out var undecidableExceptionFilter);

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Arity is not (1 or 2) || !SymbolNaming.IsInNamespace(iface, ContractNames.HandlerNamespace))
            {
                continue;
            }

            var arguments = iface.TypeArguments;

            ContractShapeModel? shape = iface.Name switch
            {
                "IPreInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.PreInterceptor, IsAsync: false, IsResultTyped: false,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), null),
                "IAsyncPreInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.PreInterceptor, IsAsync: true, IsResultTyped: false,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), null),
                "IPostInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.PostInterceptor, IsAsync: false, IsResultTyped: true,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])),
                "IAsyncPostInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.PostInterceptor, IsAsync: true, IsResultTyped: true,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])),
                "IAsyncPostInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.PostInterceptor, IsAsync: true, IsResultTyped: false,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), null),
                "IExceptionInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.ExceptionInterceptor, IsAsync: false, IsResultTyped: true,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1]),
                    exceptionFilterExpression, undecidableExceptionFilter),
                "IAsyncExceptionInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.ExceptionInterceptor, IsAsync: true, IsResultTyped: true,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1]),
                    exceptionFilterExpression, undecidableExceptionFilter),
                "IAsyncExceptionInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.ExceptionInterceptor, IsAsync: true, IsResultTyped: false,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), null,
                    exceptionFilterExpression, undecidableExceptionFilter),
                "IFinalInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.FinalInterceptor, IsAsync: false, IsResultTyped: true,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])),
                "IAsyncFinalInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.FinalInterceptor, IsAsync: true, IsResultTyped: true,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])),
                "IAsyncFinalInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.FinalInterceptor, IsAsync: true, IsResultTyped: false,
                    SymbolNaming.NormalizedTypeExpression(arguments[0]), null),
                _ => null,
            };

            if (shape is { } value)
            {
                (shapes ??= ImmutableArray.CreateBuilder<ContractShapeModel>()).Add(value);
            }
        }

        return shapes?.ToImmutable() ?? ImmutableArray<ContractShapeModel>.Empty;
    }

    /// <summary>
    ///     Reads the exception type an interceptor accepts off its
    ///     <c>IExceptionInterceptorFilter&lt;TException&gt;</c>, so the staged plan can
    ///     bake the runtime stage's filter probe in as an <c>is</c> test.
    /// </summary>
    /// <remarks>
    ///     Only the single-generic-filter shape is decidable. A type carrying the
    ///     non-generic filter without exactly one generic one has written its own
    ///     <c>Matches</c> (or has several to disambiguate by hand), and no compile-time test
    ///     reproduces it — the plan is disqualified instead of guessing, and the dispatch
    ///     asks the instance through the runtime stage.
    /// </remarks>
    internal static void ReadExceptionFilter(INamedTypeSymbol symbol, out string? filterExpression, out bool undecidable)
    {
        filterExpression = null;
        undecidable = false;

        var carriesFilter = false;
        var genericFilterCount = 0;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Name != ContractNames.ExceptionFilterContract || !SymbolNaming.IsInNamespace(iface, ContractNames.HandlerNamespace))
            {
                continue;
            }

            if (iface.Arity == 0)
            {
                carriesFilter = true;
                continue;
            }

            if (iface.Arity == 1)
            {
                genericFilterCount++;
                filterExpression = SymbolNaming.VerbatimTypeExpression(iface.TypeArguments[0]);
            }
        }

        if (!carriesFilter)
        {
            filterExpression = null;
            return;
        }

        if (genericFilterCount != 1)
        {
            filterExpression = null;
            undecidable = true;
        }
    }

    /// <summary>
    ///     Pre-computes the handler descriptors for the type's handler contracts, mirroring
    ///     the runtime descriptor builders exactly: main handlers keep their declared
    ///     message types verbatim (sync contracts first, then result-less async, then
    ///     result-producing async, no dedupe), interceptors normalize generic messages to
    ///     their definitions and dedupe per (message, result) pair with the synchronous
    ///     pattern winning.
    /// </summary>
    /// <remarks>
    ///     A generic participant definition is modelled like any other: its message
    ///     expressions carry type parameters (<c>Wrap&lt;T&gt;</c>), which is fine because
    ///     nothing emits them — the composition table matches on the definition key and
    ///     names the participant by its own unbound <c>typeof</c>, closing it over the
    ///     dispatched message's arguments at runtime. Descriptors were empty here while
    ///     they were still emitted as <c>typeof</c> arguments, which a type parameter
    ///     cannot appear in; that emission is gone. A generic <em>containing</em> type is
    ///     still refused: its parameters are not the participant's own, so closing over
    ///     the message cannot supply them.
    /// </remarks>
    internal static ImmutableArray<DescriptorModel> BuildDescriptors(INamedTypeSymbol symbol)
    {
        for (var current = symbol.ContainingType; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return ImmutableArray<DescriptorModel>.Empty;
            }
        }

        List<DescriptorModel>? mainSync = null;
        List<DescriptorModel>? mainAsyncVoid = null;
        List<DescriptorModel>? mainAsyncResult = null;
        List<DescriptorModel>? preSync = null;
        List<DescriptorModel>? preAsync = null;
        List<DescriptorModel>? postSync = null;
        List<DescriptorModel>? postAsyncTyped = null;
        List<DescriptorModel>? postAsyncAgnostic = null;
        List<DescriptorModel>? exceptionSync = null;
        List<DescriptorModel>? exceptionAsyncTyped = null;
        List<DescriptorModel>? exceptionAsyncAgnostic = null;
        List<DescriptorModel>? finalSync = null;
        List<DescriptorModel>? finalAsyncTyped = null;
        List<DescriptorModel>? finalAsyncAgnostic = null;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Arity is not (1 or 2) || !SymbolNaming.IsInNamespace(iface, ContractNames.HandlerNamespace))
            {
                continue;
            }

            var arguments = iface.TypeArguments;

            switch (iface.Name)
            {
                case "IHandler" when iface.Arity == 2:
                    (mainSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.MainHandler,
                        SymbolNaming.VerbatimTypeExpression(arguments[0]),
                        SymbolNaming.VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncHandler" when iface.Arity == 1:
                    (mainAsyncVoid ??= []).Add(new DescriptorModel(
                        DescriptorKind.MainHandler,
                        SymbolNaming.VerbatimTypeExpression(arguments[0]),
                        EmittedExpressions.ValueTask));
                    break;
                case "IAsyncHandler" when iface.Arity == 2:
                    (mainAsyncResult ??= []).Add(new DescriptorModel(
                        DescriptorKind.MainHandler,
                        SymbolNaming.VerbatimTypeExpression(arguments[0]),
                        EmittedExpressions.ValueTask + "<" + SymbolNaming.VerbatimTypeExpression(arguments[1]) + ">"));
                    break;

                case "IPreInterceptor" when iface.Arity == 1:
                    (preSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.PreInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), null));
                    break;
                case "IAsyncPreInterceptor" when iface.Arity == 1:
                    (preAsync ??= []).Add(new DescriptorModel(
                        DescriptorKind.PreInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), null));
                    break;

                case "IPostInterceptor" when iface.Arity == 2:
                    (postSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.PostInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncPostInterceptor" when iface.Arity == 2:
                    (postAsyncTyped ??= []).Add(new DescriptorModel(
                        DescriptorKind.PostInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncPostInterceptor" when iface.Arity == 1:
                    (postAsyncAgnostic ??= []).Add(new DescriptorModel(
                        DescriptorKind.PostInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), "object"));
                    break;

                case "IExceptionInterceptor" when iface.Arity == 2:
                    (exceptionSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.ExceptionInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncExceptionInterceptor" when iface.Arity == 2:
                    (exceptionAsyncTyped ??= []).Add(new DescriptorModel(
                        DescriptorKind.ExceptionInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncExceptionInterceptor" when iface.Arity == 1:
                    (exceptionAsyncAgnostic ??= []).Add(new DescriptorModel(
                        DescriptorKind.ExceptionInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), "object"));
                    break;

                case "IFinalInterceptor" when iface.Arity == 2:
                    (finalSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.FinalInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncFinalInterceptor" when iface.Arity == 2:
                    (finalAsyncTyped ??= []).Add(new DescriptorModel(
                        DescriptorKind.FinalInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), SymbolNaming.VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncFinalInterceptor" when iface.Arity == 1:
                    (finalAsyncAgnostic ??= []).Add(new DescriptorModel(
                        DescriptorKind.FinalInterceptor, SymbolNaming.NormalizedTypeExpression(arguments[0]), "object"));
                    break;
            }
        }

        var result = ImmutableArray.CreateBuilder<DescriptorModel>();

        // Main handlers: runtime builder order, no dedupe.
        AppendAll(result, mainSync);
        AppendAll(result, mainAsyncVoid);
        AppendAll(result, mainAsyncResult);

        // Interceptors: runtime builder order with first-wins dedupe per (message, result).
        AppendDeduped(result, preSync, preAsync, null);
        AppendDeduped(result, postSync, postAsyncTyped, postAsyncAgnostic);
        AppendDeduped(result, exceptionSync, exceptionAsyncTyped, exceptionAsyncAgnostic);
        AppendDeduped(result, finalSync, finalAsyncTyped, finalAsyncAgnostic);

        return result.ToImmutable();
    }

    internal static void AppendAll(ImmutableArray<DescriptorModel>.Builder result, List<DescriptorModel>? bucket)
    {
        if (bucket is null)
        {
            return;
        }

        foreach (var descriptor in bucket)
        {
            result.Add(descriptor);
        }
    }

    internal static void AppendDeduped(
        ImmutableArray<DescriptorModel>.Builder result,
        List<DescriptorModel>? first,
        List<DescriptorModel>? second,
        List<DescriptorModel>? third)
    {
        if (first is null && second is null && third is null)
        {
            return;
        }

        var seen = new HashSet<(string Message, string? Result)>();

        AppendBucket(result, first, seen);
        AppendBucket(result, second, seen);
        AppendBucket(result, third, seen);

        static void AppendBucket(
            ImmutableArray<DescriptorModel>.Builder result,
            List<DescriptorModel>? bucket,
            HashSet<(string Message, string? Result)> seen)
        {
            if (bucket is null)
            {
                return;
            }

            foreach (var descriptor in bucket)
            {
                if (seen.Add((descriptor.MessageTypeExpression, descriptor.ResultTypeExpression)))
                {
                    result.Add(descriptor);
                }
            }
        }
    }

    /// <summary>
    ///     The event messages this type's subscriber contracts name, limited to the ones that
    ///     carry no module marker of their own.
    /// </summary>
    /// <remarks>
    ///     An <c>IEventHandler&lt;T&gt;</c> signature is not evidence pointing at a message —
    ///     it is what makes one. A plain domain type means nothing to the generator until a
    ///     subscriber is written for it, and its own declaration is never visited (it has no
    ///     base list to be selected by), so this is where its model has to be born. A message
    ///     that already carries <c>IEvent</c> is skipped: it is registrable on its own terms
    ///     and deriving it again would only produce a duplicate for the pipeline to drop.
    /// </remarks>
    internal static ImmutableArray<INamedTypeSymbol> GetDerivedEventMessages(INamedTypeSymbol symbol)
    {
        ImmutableArray<INamedTypeSymbol>.Builder? derived = null;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface is not { Arity: 1, Name: ContractNames.EventHandlerContract }
                || !SymbolNaming.IsInNamespace(iface, ContractNames.EventMarkerNamespace)
                || iface.TypeArguments[0] is not INamedTypeSymbol message)
            {
                continue;
            }

            ParticipantAttributes.GetMarkers(message, out var isCommand, out var isQuery, out var isEvent);

            if (isCommand || isQuery || isEvent)
            {
                continue;
            }

            derived ??= ImmutableArray.CreateBuilder<INamedTypeSymbol>();

            if (!derived.Contains(message, SymbolEqualityComparer.Default))
            {
                derived.Add(message);
            }
        }

        return derived?.ToImmutable() ?? ImmutableArray<INamedTypeSymbol>.Empty;
    }
}
