using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
/// Reads a type's Ergosfare contracts off its interface list.
/// </summary>
/// <remarks>
/// Which pipeline stages it takes part in and for which messages — the descriptors — what
/// shape its handler members have, and, for a message, whether it can be dispatched and with
/// what result.
/// </remarks>
internal static class ContractReader
{
    /// <summary>
    /// Reports whether a composition can be computed for this type.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <param name="descriptors">The type's own handler contracts.</param>
    /// <returns><c>true</c> when it is a message shape.</returns>
    /// <remarks>
    /// Wider than <see cref="IsDispatchableMessage"/>: an abstract base or an interface is
    /// never dispatched itself, but it is where the frozen table's ancestor ladder lands when
    /// a message this compilation never saw — a proxy, a type hidden from discovery — is
    /// dispatched. A generic message definition is a shape too, and needs no closed
    /// instantiation: the table keys it by its definition and the lookup normalizes a runtime
    /// <c>Wrap&lt;int&gt;</c> to <c>Wrap&lt;&gt;</c>, so one entry serves every
    /// instantiation. A generic containing type is still refused — see
    /// <see cref="BuildDescriptors"/>.
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

    /// <summary>
    /// Reports whether a type can appear as a dispatched message instance.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <param name="descriptors">The type's own handler contracts.</param>
    /// <returns>
    /// <c>true</c> for a concrete, fully closed class or struct carrying no handler contract.
    /// </returns>
    /// <remarks>
    /// Only such a type gets dispatch roots: an abstract type or an interface never carries a
    /// runtime message's type, a handler is never dispatched, and an open generic cannot be
    /// rooted.
    /// </remarks>
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
    /// Collects the result roots a dispatchable message needs.
    /// </summary>
    /// <param name="symbol">The message to read.</param>
    /// <returns>One entry per distinct result contract the message closes.</returns>
    /// <remarks>
    /// <c>ICommand&lt;T&gt;</c> and <c>IQuery&lt;T&gt;</c> feed the result path;
    /// <c>IStreamQuery&lt;T&gt;</c> feeds the streaming one.
    /// </remarks>
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
    /// Collects the interceptor contracts a type implements, undeduped.
    /// </summary>
    /// <param name="symbol">The type to read.</param>
    /// <returns>
    /// One shape per contract, or empty when the type cannot be named by a staged plan.
    /// </returns>
    /// <remarks>
    /// The counterpart of <see cref="BuildDescriptors"/>'s interceptor walk, keeping each
    /// contract separate along with whether it is asynchronous and whether it names a result
    /// — which is what selecting a call's contract turns on.
    /// </remarks>
    internal static ImmutableArray<ContractShapeModel> BuildContractShapes(INamedTypeSymbol symbol)
    {
        // Arity alone settles nothing: a closed constructed participant has the same arity as
        // the definition it came from, and its contracts name concrete types. What rules a
        // type out here is an unbound level anywhere in its chain, because a staged plan has
        // to name the participant and a name with an open level cannot be written.
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
    /// Reads the exception type an interceptor accepts.
    /// </summary>
    /// <param name="symbol">The interceptor to read.</param>
    /// <param name="filterExpression">
    /// The accepted exception type, or <c>null</c> when the interceptor declares no filter or
    /// the filter cannot be reproduced.
    /// </param>
    /// <param name="undecidable">Set when the filter cannot be reproduced.</param>
    /// <remarks>
    /// A single <c>IExceptionInterceptorFilter&lt;TException&gt;</c> lets a staged plan bake
    /// the runtime stage's probe in as an <c>is</c> test. A type carrying the non-generic
    /// filter without exactly one generic one has written its own <c>Matches</c>, or has
    /// several filters to disambiguate by hand, and no compile-time test reproduces that —
    /// the plan is dropped rather than guessed at, and the dispatch asks the instance through
    /// the runtime stage.
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
    /// Builds the descriptors for a type's handler contracts.
    /// </summary>
    /// <param name="symbol">The type to read.</param>
    /// <returns>
    /// Its descriptors in the order the runtime builds them, or empty when the type is nested
    /// in a generic one.
    /// </returns>
    /// <remarks>
    /// The order and the deduping match the runtime descriptor builders exactly. A main
    /// handler keeps its declared message type verbatim — synchronous contracts first, then
    /// result-less asynchronous, then result-producing, with no deduping. An interceptor
    /// normalizes a generic message to its definition and is deduped per message and result,
    /// where the synchronous contract wins.
    /// <para>
    /// A generic participant definition is read like any other, its message expressions
    /// carrying type parameters such as <c>Wrap&lt;T&gt;</c>: nothing emits those, since the
    /// composition table matches on the definition key and names the participant by its own
    /// unbound <c>typeof</c>, closing it over the dispatched message's arguments. A generic
    /// containing type is refused, because its parameters are not the participant's own and
    /// closing over the message cannot supply them.
    /// </para>
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

        // Main handlers, in the runtime builder's order and with nothing dropped.
        AppendAll(result, mainSync);
        AppendAll(result, mainAsyncVoid);
        AppendAll(result, mainAsyncResult);

        // Interceptors, in that same order, keeping the first contract per message and result.
        AppendDeduped(result, preSync, preAsync, null);
        AppendDeduped(result, postSync, postAsyncTyped, postAsyncAgnostic);
        AppendDeduped(result, exceptionSync, exceptionAsyncTyped, exceptionAsyncAgnostic);
        AppendDeduped(result, finalSync, finalAsyncTyped, finalAsyncAgnostic);

        return result.ToImmutable();
    }

    /// <summary>
    /// Appends a bucket's descriptors, keeping every one.
    /// </summary>
    /// <param name="result">The builder to append to.</param>
    /// <param name="bucket">The descriptors to append, or <c>null</c> when there are none.</param>
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

    /// <summary>
    /// Appends one stage's buckets in order, keeping the first descriptor per message and
    /// result.
    /// </summary>
    /// <param name="result">The builder to append to.</param>
    /// <param name="first">The bucket that wins a tie, or <c>null</c> when empty.</param>
    /// <param name="second">The next bucket, or <c>null</c> when empty.</param>
    /// <param name="third">The last bucket, or <c>null</c> when empty.</param>
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
    /// Collects the event messages a type's subscriber contracts name, for the ones carrying
    /// no module marker of their own.
    /// </summary>
    /// <param name="symbol">The subscriber to read.</param>
    /// <returns>The named messages, each once.</returns>
    /// <remarks>
    /// An <c>IEventHandler&lt;T&gt;</c> signature is not evidence pointing at a message — it
    /// is what makes one. A plain domain type means nothing here until a subscriber is
    /// written for it, and its own declaration is never visited, having no base list to be
    /// selected by, so this is where its model has to be born. A message already carrying
    /// <c>IEvent</c> is skipped: it is registrable on its own terms, and naming it again
    /// would only produce a duplicate to drop.
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
