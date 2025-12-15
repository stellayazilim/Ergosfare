using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Queries.Abstractions;
// ReSharper disable ClassNeverInstantiated.Global

namespace Stella.Ergosfare.Queries.Test.__stubs__;

/// <summary>
/// Handles <see cref="StubNonGenericStringResultQuery"/> and returns a string result.
/// Used for testing non-generic query handling.
/// </summary>
public class StubNonGenericStringResultQueryHandler: IQueryHandler<StubNonGenericStringResultQuery, string>
{
    /// <summary>
    /// Indicates whether the handler was called.
    /// </summary>   
    public static bool IsCalled;
    
    /// <summary>
    /// Handles the specified query and returns a string result.
    /// </summary>
    /// <param name="message">The query message.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A task representing the asynchronous operation, with an empty string result.</returns>
    public Task<string> HandleAsync(StubNonGenericStringResultQuery message, IExecutionContext context)
    {
        IsCalled = true;
        return Task.FromResult(string.Empty);
    }
}



/// <summary>
/// Handles <see cref="StubNonGenericStreamStringResultQuery"/> and streams string results asynchronously.
/// Used for testing non-generic streaming query handling.
/// </summary>
public class StubNonGenericStreamStringResultQueryHandler: IStreamQueryHandler<StubNonGenericStreamStringResultQuery, string>
{
    /// <summary>
    /// Indicates whether the handler was called.
    /// </summary>
    public static bool IsCalled;
    
    /// <summary>
    /// Streams the results of the specified query asynchronously.
    /// </summary>
    /// <param name="message">The query message.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>An asynchronous stream of strings.</returns>
    public async IAsyncEnumerable<string> StreamAsync(StubNonGenericStreamStringResultQuery message, IExecutionContext context)
    {
        IsCalled = true;
        await Task.Delay(1, context.CancellationToken);
        yield return "Foo";
        await Task.Delay(1, context.CancellationToken);
        yield return "Bar";
        await Task.Delay(1, context.CancellationToken);
        yield return "Baz";
    }
}