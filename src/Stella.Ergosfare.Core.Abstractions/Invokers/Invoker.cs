using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions.Invokers;


/// <summary>
/// Provides a common abstract base for all invoker types, ensuring they
/// have access to shared message dependencies.
/// </summary>
internal abstract class AbstractInvoker(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService,
    IServiceProvider serviceProvider)
{
    protected IResultAdapterService? ResultAdapterService => resultAdapterService;

    /// <summary>
    /// The provider of the scope the current dispatch runs in; handler references
    /// resolve their instances from it at invocation time.
    /// </summary>
    protected IServiceProvider ServiceProvider { get; } = serviceProvider;
    /// <summary>
    /// Gets the total number of pre-interceptors (direct and indirect are merged) for the associated message.
    /// </summary>
    protected ushort PreInterceptorCount = (ushort) messageDependencies.PreInterceptors.Count;

    /// <summary>
    /// Gets the total number of post-interceptors (direct and indirect are merged) for the associated message.
    /// </summary>
    protected ushort PostInterceptorCount = (ushort) messageDependencies.PostInterceptors.Count;

    /// <summary>
    /// Gets the total number of exception interceptors (direct and indirect are merged) for the associated message.
    /// </summary>
    protected ushort ExceptionInterceptorCount = (ushort) messageDependencies.ExceptionInterceptors.Count;

    /// <summary>
    /// Gets the total number of final interceptors (direct and indirect are merged) for the associated message.
    /// </summary>
    protected ushort FinalInterceptorCount = (ushort) messageDependencies.FinalInterceptors.Count;
    
    /// <summary>
    /// Awaits a result returned by an interceptor, handling both synchronous and asynchronous cases.
    /// </summary>
    /// <param name="result">
    /// The object returned by an interceptor. Can be:
    /// <list type="bullet">
    ///   <item>A boxed <see cref="ValueTask"/> (no result), which will be awaited and converted to <c>null</c>.</item>
    ///   <item>A boxed <see cref="ValueTask{TResult}"/> of <see cref="object"/>, which will be awaited and its result returned.</item>
    ///   <item>A <see cref="Task"/> / <see cref="Task{TResult}"/> (interceptors implemented against the object-typed root), awaited likewise.</item>
    ///   <item>A regular object, which is returned as-is.</item>
    /// </list>
    /// </param>
    /// <returns>
    /// The awaited result as <see cref="object"/>. If the input carried no result, returns <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method is used to unify handling of interceptor return values across pre-, post-, exception-, and final-interceptor pipelines,
    /// allowing synchronous and asynchronous interceptors to be treated consistently. Each boxed
    /// <see cref="ValueTask"/> arrives here exactly once and is awaited exactly once.
    /// </remarks>
    protected async ValueTask<object?> ConvertTask(object? result)
    {
        switch (result)
        {
            case ValueTask<object?> valueTaskT:
                return await valueTaskT;
            case ValueTask valueTask:
                await valueTask;
                return null;
            case Task<object?> taskT:
                return await taskT;
            case Task task:
                await task;
                return null;
            default:
                // safely return: it's not an async carrier
                return result;
        }
    }
    
    /// <summary>
    /// Gets the message dependencies that provide access to registered
    /// handlers, interceptors, and related metadata required for invocation.
    /// </summary>
    protected  IMessageDependencies MessageDependencies { get; } = messageDependencies;
}