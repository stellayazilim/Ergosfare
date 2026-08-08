namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Implemented by pooled execution contexts; <see cref="ExecutionContextScope.Dispose"/>
/// and the dispatch paths return contexts through it.
/// </summary>
internal interface IPoolReturnable
{
    /// <summary>Resets the instance and returns it to its pool.</summary>
    void ReturnToPool();
}
