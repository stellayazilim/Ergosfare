using System;
using System.Collections.Generic;

namespace Ergosfare.Core.Abstractions;


/// <summary>
/// Provides a central service to manage multiple <see cref="IResultAdapter"/> instances 
/// and to extract exceptions from arbitrary result objects.
/// </summary>
public interface IResultAdapterService
{
    /// <summary>
    /// Registers a new <see cref="IResultAdapter"/> to the service.
    /// Adapters are evaluated in the order they are added when looking up exceptions.
    /// </summary>
    /// <param name="adapter">The adapter instance to register. Must not be null.</param>
    void AddAdapter(IResultAdapter adapter);
    
    IEnumerable<IResultAdapter> GetAdapters();
    
    /// <summary>
    /// Attempts to find an exception inside the given <paramref name="result"/> object
    /// using the registered adapters.
    /// </summary>
    /// <param name="result">The result object to examine. Can be any type, including null.</param>
    /// <returns>
    /// The extracted <see cref="Exception"/> if one is found by any adapter; 
    /// otherwise, <c>null</c>.
    /// </returns>
    Exception? LookupException(object? result);
}