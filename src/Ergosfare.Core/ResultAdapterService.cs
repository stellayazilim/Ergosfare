using Ergosfare.Core.Abstractions;

namespace Ergosfare.Core;



/// <summary>
/// Implementation of <see cref="IResultAdapterService"/> that manages a collection
/// of <see cref="IResultAdapter"/> instances and provides exception lookup functionality.
/// </summary>
/// <remarks>
/// This service allows the pipeline to remain agnostic of the result type while still
/// extracting exceptions when necessary. Multiple adapters can be registered to handle
/// different result styles (e.g., plain values, FluentResults, OneOf types, or custom wrappers).
/// The adapters are evaluated in the order they were added, and the first adapter capable
/// of adapting the result will be used to extract an exception.
/// </remarks>
public sealed class ResultAdapterService: IResultAdapterService
{
    private readonly List<IResultAdapter> _resultAdapters = new();
    
    
    /// <summary>
    /// Registers a new result adapter.
    /// </summary>
    /// <param name="adapter">The adapter instance to register. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="adapter"/> is null.</exception>
    public void AddAdapter(IResultAdapter adapter)
    {
        if (adapter == null) throw new ArgumentNullException(nameof(adapter));
        _resultAdapters.Add(adapter);
    }

    
    public IEnumerable<IResultAdapter> GetAdapters() => _resultAdapters;
    /// <summary>
    /// Iterates over the registered adapters to find the first one that can handle
    /// the given <paramref name="result"/> and extract an exception.
    /// </summary>
    /// <param name="result">The result object to examine. Can be null.</param>
    /// <returns>
    /// The extracted <see cref="Exception"/> if found by any adapter; otherwise <c>null</c>.
    /// </returns>
    public Exception? LookupException(object? result)
    {
        if (result is null) return null;
        foreach (var adapter in _resultAdapters)
        {
            if (adapter.CanAdapt(result))
            {
               return adapter.TryGetException(result, out var exception) ? exception : null;
            }
        }
        return null; // no adapter claimed it
    }
}