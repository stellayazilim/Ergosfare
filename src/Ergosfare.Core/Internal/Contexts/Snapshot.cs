using Ergosfare.Core.Abstractions;

namespace Ergosfare.Core.Internal.Contexts;


/// <summary>
/// Default implementation of <see cref="ISnapshot{TResult}"/>.
/// Executes the provided action immediately and captures its result.
/// </summary>
/// <typeparam name="TResult">The type of the captured result of this snapshot step.</typeparam>
public class Snapshot<TResult>: ISnapshot<TResult>
{
    /// <inheritdoc />
    public TResult Result { get; set; } = default!;
    
    
    
    /// <summary>
    /// Creates a new snapshot by executing the given asynchronous action immediately
    /// and capturing its result. The snapshot result is specific to this step and 
    /// does not represent the overall pipeline output.
    /// </summary>
    /// <param name="action">The asynchronous action to execute and snapshot.</param>
    public Snapshot(Func<Task<TResult>> action)
    {
        // Blocks until the task completes and stores its result.
        Result = action().GetAwaiter().GetResult();
    }
}