
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Declares the exception type an interceptor accepts. Matching follows <c>catch</c>
/// semantics: <typeparamref name="TException"/> and every type derived from it.
/// </summary>
/// <typeparam name="TException">The exception type this interceptor accepts.</typeparam>
/// <remarks>
/// This contract carries the exception type into the generated pipelines: the source
/// generator reads <typeparamref name="TException"/> off it and bakes the same test into
/// the emitted plan as a compile-time <c>is</c> check, so both registration axes filter
/// identically.
/// </remarks>
public interface IExceptionInterceptorFilter<TException> : IExceptionInterceptorFilter
    where TException : Exception
{
    /// <inheritdoc />
    bool IExceptionInterceptorFilter.Matches(Exception exception) => exception is TException;
}
