// Stream completion is required even when dispatch fails.
#pragma warning disable ERGOEXP003
using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions.Streaming;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Planning;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Internal.Extensions;

/// <summary>Invokes a selected generated plan under an existing execution context.</summary>
internal static class MessageDispatchExecutionExtensions
{
    internal static ValueTask SendPlan(this MessageDispatchEngine engine, object message, ErgosfareContext context, IServiceProvider provider,
        GroupSet? groups)
    {
        if (message is ErgosfareStream stream)
            return SendAndEnd(engine, stream, context, provider, groups);
        return engine.SendCore(message, context, provider, groups);
    }

    private static ValueTask SendCore(this MessageDispatchEngine engine, object message, ErgosfareContext context, IServiceProvider provider,
        GroupSet? groups)
    {
        var requested = groups.ToDispatchGroups();
        var type = message.GetType();
        var plan = engine.Catalog.FindPlan(type, null, 0, requested);
        if (plan is null)
            engine.ValidatePlan(type, GeneratedPlanRegistry.FindStagedVoidPlan(type, requested)
                ?? GeneratedPlanRegistry.FindFilteredVoidPlan(type), requested, broadcast: false);
        return ((IPipelineExecutor)plan!).Execute(message, context, provider, requested);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private static async ValueTask SendAndEnd(this MessageDispatchEngine engine, ErgosfareStream message, ErgosfareContext context,
        IServiceProvider provider, GroupSet? groups)
    {
        Exception? error = null;
        try { await engine.SendCore(message, context, provider, groups).ConfigureAwait(false); }
        catch (Exception e) { error = e; throw; }
        finally
        {
            message.EndDispatch(error);
            await message.WaitForProducerAsync().ConfigureAwait(false);
        }
    }

    internal static ValueTask<TResult> SendPlan<TResult>(this MessageDispatchEngine engine, object message, ErgosfareContext context,
        IServiceProvider provider, GroupSet? groups)
    {
        if (message is ErgosfareStream stream)
            return SendAndEnd<TResult>(engine, stream, context, provider, groups);
        return engine.SendCore<TResult>(message, context, provider, groups);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private static async ValueTask<TResult> SendAndEnd<TResult>(this MessageDispatchEngine engine, ErgosfareStream message, ErgosfareContext context,
        IServiceProvider provider, GroupSet? groups)
    {
        Exception? error = null;
        try { return await engine.SendCore<TResult>(message, context, provider, groups).ConfigureAwait(false); }
        catch (Exception e) { error = e; throw; }
        finally
        {
            message.EndDispatch(error);
            await message.WaitForProducerAsync().ConfigureAwait(false);
        }
    }

    private static ValueTask<TResult> SendCore<TResult>(this MessageDispatchEngine engine, object message, ErgosfareContext context,
        IServiceProvider provider, GroupSet? groups)
    {
        var requested = groups.ToDispatchGroups();
        var type = message.GetType();
        var plan = engine.Catalog.FindPlan(type, typeof(TResult), 1, requested);
        if (plan is null)
            engine.ValidatePlan(type, GeneratedPlanRegistry.FindStagedResultPlan(type, typeof(TResult), requested)
                ?? GeneratedPlanRegistry.FindFilteredResultPlan(type, typeof(TResult)), requested, broadcast: false);
        return ((IPipelineExecutor<TResult>)plan!).Execute(message, context, provider, requested);
    }

    internal static ValueTask BroadcastPlan(this MessageDispatchEngine engine, object message, ErgosfareContext context, IServiceProvider provider,
        GroupSet? groups)
    {
        var requested = groups.ToDispatchGroups();
        var type = message.GetType();
        var plan = engine.Catalog.FindPlan(type, null, 2, requested);
        if (plan is null)
            engine.ValidatePlan(type, GeneratedPlanRegistry.FindBroadcastPlan(type, requested)
                ?? GeneratedPlanRegistry.FindFilteredBroadcastPlan(type), requested, broadcast: true);
        return ((IPipelineExecutor)plan!).Execute(message, context, provider, requested);
    }

    internal static IAsyncEnumerable<TResult> StreamPlan<TResult>(this MessageDispatchEngine engine, object message, ErgosfareContext context,
        IServiceProvider provider, GroupSet? groups)
    {
        var requested = groups.ToDispatchGroups();
        var type = message.GetType();
        if (requested.Count != 0)
            throw MessageDispatchDiagnosticsExtensions.Missing(type, UnplannedDispatchReason.UnplannedGroupSet);
        var plan = engine.Catalog.FindPlan(type, typeof(TResult), 3, requested);
        if (plan is null)
            engine.ValidatePlan(type, GeneratedPlanRegistry.FindStagedStreamPlan(type, typeof(TResult)), requested, broadcast: false);
        return ((ICompiledStreamPlan<TResult>)plan!).Execute(message, context, provider, context.CancellationToken);
    }
}
