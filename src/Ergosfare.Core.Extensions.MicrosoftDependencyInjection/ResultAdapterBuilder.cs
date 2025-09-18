using System.Reflection;
using Ergosfare.Core.Abstractions;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides a fluent API for registering <see cref="IResultAdapter"/> implementations
/// into an <see cref="IResultAdapterService"/>.
/// </summary>
/// <remarks>
/// This builder supports:
/// - Registering adapters by generic type.
/// - Registering adapters by <see cref="Type"/>.
/// - Registering all adapters from a given <see cref="Assembly"/>.
/// 
/// Each registration creates a new adapter instance via <see cref="Activator.CreateInstance(Type)"/>.
/// </remarks>
public class ResultAdapterBuilder(IResultAdapterService resultAdapterService)
{
    /// <summary>
    /// Registers a result adapter of type <typeparamref name="TAdapter"/>.
    /// </summary>
    /// <typeparam name="TAdapter">The adapter type implementing <see cref="IResultAdapter"/>.</typeparam>
    /// <returns>The current <see cref="ResultAdapterBuilder"/> instance for chaining.</returns>
    /// <remarks>
    /// A new instance of <typeparamref name="TAdapter"/> is created via its parameterless constructor.
    /// </remarks>
    public ResultAdapterBuilder Register<TAdapter>() where TAdapter : IResultAdapter, new()
    {
        // Create a new instance of the adapter and add it to the service
        resultAdapterService.AddAdapter(new TAdapter());
        return this;
    }

    /// <summary>
    /// Registers a result adapter by its <see cref="Type"/>.
    /// </summary>
    /// <param name="adapter">The adapter type to register.</param>
    /// <returns>The current <see cref="ResultAdapterBuilder"/> instance for chaining.</returns>
    /// <remarks>
    /// The adapter type must implement <see cref="IResultAdapter"/> and have a parameterless constructor.
    /// </remarks>
    public ResultAdapterBuilder Register(Type adapter) 
    {
        // Use Activator to create an instance dynamically
        if (Activator.CreateInstance(adapter) is IResultAdapter resultAdapter)
            resultAdapterService.AddAdapter(resultAdapter);
        
        return this;
    }

    /// <summary>
    /// Registers all <see cref="IResultAdapter"/> implementations found in the given assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for adapter types.</param>
    /// <returns>The current <see cref="ResultAdapterBuilder"/> instance for chaining.</returns>
    /// <remarks>
    /// This scans for non-abstract, concrete classes that implement <see cref="IResultAdapter"/>.
    /// Each matching type is instantiated and registered.
    /// </remarks>
    public ResultAdapterBuilder RegisterFromAssembly(Assembly assembly)
    {
        // Scan all types in the assembly and register those that implement IResultAdapter
        foreach (var adapter in assembly.GetTypes()
                   .Where(t => t.IsAssignableTo(typeof(IResultAdapter)) && t is { IsClass: true, IsAbstract: false }))
        {
            if (Activator.CreateInstance(adapter) is IResultAdapter resultAdapter)
                resultAdapterService.AddAdapter(resultAdapter);
        }

        return this;
    }
}
