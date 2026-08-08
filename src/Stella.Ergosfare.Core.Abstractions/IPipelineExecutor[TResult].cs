namespace Stella.Ergosfare.Core.Abstractions;

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
