using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides a builder for selecting the query constructs this container runs from the
/// compiled composition table.
/// </summary>
/// <param name="compositions">
/// The container's composition catalog, told which constructs this registration selects.
/// </param>
public sealed class QueryModuleBuilder(FrozenCompositionCatalog compositions)
{
    private readonly FrozenCompositionCatalog _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));


    /// <summary>
    /// Registers a query construct.
    /// </summary>
    /// <typeparam name="TQuery">The type to register. Must be a query construct.</typeparam>
    /// <returns>The current <see cref="QueryModuleBuilder"/> instance for fluent chaining.</returns>
    public QueryModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TQuery>() where TQuery : IQuery
    {
        Register(typeof(TQuery));
        return this;
    }

    /// <summary>
    /// Registers a query construct — a query, or one of the handlers and interceptors
    /// serving queries (their contracts carry the module marker too).
    /// </summary>
    /// <param name="queryType">The <see cref="Type"/> to register.</param>
    /// <returns>The current <see cref="QueryModuleBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="NotSupportedException">Thrown if the type is not a query construct.</exception>
    public QueryModuleBuilder Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type queryType)
    {
        if (!queryType.IsAssignableTo(typeof(IQuery)))
            throw new NotSupportedException($"The given type '{queryType.Name}' is not a query construct and cannot be registered.");

        _compositions.Select(queryType);
        return this;
    }

    /// <summary>
    /// Registers a batch of pipeline participants — the bulk path source-generated
    /// registration uses.
    /// </summary>
    /// <remarks>
    /// No module assertion here: the generator has already partitioned its discoveries by
    /// module, and not every participant contract carries the module marker (the modifying
    /// interceptor shapes are declared purely over the core contracts).
    /// <see cref="Register(Type)"/> keeps the assertion, since a hand-written registration
    /// is where a wrong-module type actually surfaces.
    /// </remarks>
    /// <param name="participantTypes">The handler and interceptor types to register.</param>
    /// <returns>The current <see cref="QueryModuleBuilder"/> instance for fluent chaining.</returns>
    public QueryModuleBuilder RegisterParticipants(IEnumerable<Type> participantTypes)
    {
        _compositions.Select(participantTypes);
        return this;
    }
}
