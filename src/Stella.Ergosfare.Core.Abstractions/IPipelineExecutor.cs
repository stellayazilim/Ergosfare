using System;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A message pipeline closed over its concrete message type, built once per message type
/// and cached process-wide. <see cref="Execute{TResult}"/> receives the message as
/// <see cref="object"/> and performs a single cast to the concrete type internally, so the
/// handler is always invoked through its typed member — no object-typed bridge, no boxing
/// of the handler's <see cref="ValueTask"/>.
/// </summary>
/// <remarks>
/// This is the dispatch seam source-generated code will eventually implement directly;
/// the runtime builds executors reflectively (one generic instantiation per message type)
/// as the fallback.
/// </remarks>
public interface IPipelineExecutor
{
    /// <summary>
    /// Executes the void pipeline for <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message instance; its runtime type is the executor's closed message type (or derived).</param>
    /// <param name="context">The execution context for this dispatch.</param>
    /// <param name="serviceProvider">The provider of the scope the dispatch runs in.</param>
    ValueTask Execute(object message, IExecutionContext context, IServiceProvider serviceProvider);
}

/// <summary>
/// A result-producing message pipeline closed over its concrete message type; see
/// <see cref="IPipelineExecutor"/>.
/// </summary>
/// <typeparam name="TResult">The result type produced by the pipeline.</typeparam>
public interface IPipelineExecutor<TResult>
{
    /// <inheritdoc cref="IPipelineExecutor.Execute"/>
    ValueTask<TResult> Execute(object message, IExecutionContext context, IServiceProvider serviceProvider);
}
