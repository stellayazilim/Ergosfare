using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides a builder for registering query types in the message registry
/// as part of the query module configuration.
/// </summary>
public sealed class QueryModuleBuilder(IMessageRegistry messageRegistry)
{
    private readonly IMessageRegistry _messageRegistry = messageRegistry ?? throw new ArgumentNullException(nameof(messageRegistry));

    
    /// <summary>
    /// Registers a specific query type <typeparamref name="TQuery"/> in the message registry.
    /// </summary>
    /// <typeparam name="TQuery">The query type to register. Must implement <see cref="IQuery"/>.</typeparam>
    /// <returns>The current <see cref="QueryModuleBuilder"/> instance for fluent chaining.</returns>
    public QueryModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TQuery>() where TQuery : IQuery
    {
        Register(typeof(TQuery));
        return this;
    }

    /// <summary>
    /// Registers a query type by its <see cref="Type"/> in the message registry.
    /// </summary>
    /// <param name="queryType">The <see cref="Type"/> of the query to register.</param>
    /// <returns>The current <see cref="QueryModuleBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="NotSupportedException">Thrown if the type does not implement <see cref="IQuery"/>.</exception>
    public QueryModuleBuilder Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type queryType)
    {
        if (!queryType.IsAssignableTo(typeof(IQuery)))    
            throw new NotSupportedException($"The given type '{queryType.Name}' is not a query construct and cannot be registered.");
        
        _messageRegistry.Register(queryType);
        return this;

    }

    /// <summary>
    /// Registers pre-built handler descriptors, bypassing reflection-based descriptor
    /// construction — the registration path used by source-generated code.
    /// </summary>
    /// <param name="descriptors">The descriptors to register; every handler type must be a query construct.</param>
    /// <returns>The current <see cref="QueryModuleBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="NotSupportedException">Thrown when a descriptor's handler type is not a query construct.</exception>
    public QueryModuleBuilder RegisterDescriptors(IEnumerable<IHandlerDescriptor> descriptors)
    {
        var accepted = new List<IHandlerDescriptor>();

        foreach (var descriptor in descriptors)
        {
            if (!descriptor.HandlerType.IsAssignableTo(typeof(IQuery)))
            {
                throw new NotSupportedException($"The given type '{descriptor.HandlerType.Name}' is not a query construct and cannot be registered.");
            }

            accepted.Add(descriptor);
        }

        _messageRegistry.RegisterDescriptors(accepted);
        return this;
    }

    /// <summary>
    /// Registers all query types found in the specified assembly.
    /// </summary>
    /// <param name="assembly">The <see cref="Assembly"/> to scan for query types.</param>
    /// <returns>The current <see cref="QueryModuleBuilder"/> instance for fluent chaining.</returns>
    [RequiresUnreferencedCode("Assembly scanning discovers query types via reflection; trimming may remove them. Register queries explicitly (or use source-generated registration) in trimmed or AOT applications.")]
    public QueryModuleBuilder RegisterFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(IQuery))))
        {
            _messageRegistry.Register(type);
        }
        
        return this;
    }
}