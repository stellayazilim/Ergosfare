using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Selects which of the compiled query constructs this container runs.
/// </summary>
/// <param name="compositions">
/// The catalog this builder records the container's selection in.
/// </param>
/// <exception cref="ArgumentNullException"><paramref name="compositions"/> is <c>null</c>.</exception>
public sealed class QueryModuleBuilder(FrozenCompositionCatalog compositions)
{
    private readonly FrozenCompositionCatalog _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));

    /// <summary>
    /// Registers one query construct.
    /// </summary>
    /// <typeparam name="TQuery">
    /// The type to register: a query, or one of the handlers and interceptors that serve
    /// queries.
    /// </typeparam>
    /// <returns>The same builder, so calls can be chained.</returns>
    public QueryModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TQuery>() where TQuery : IQuery
    {
        Register(typeof(TQuery));
        return this;
    }

    /// <summary>
    /// Registers one query construct.
    /// </summary>
    /// <param name="queryType">
    /// The type to register: a query, or one of the handlers and interceptors that serve
    /// queries — their contracts carry the module's marker too.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="NotSupportedException">
    /// The type does not belong to the query module.
    /// </exception>
    public QueryModuleBuilder Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type queryType)
    {
        if (!queryType.IsAssignableTo(typeof(IQuery)))
            throw new NotSupportedException($"The given type '{queryType.Name}' is not a query construct and cannot be registered.");

        _compositions.Select(queryType);
        return this;
    }

    /// <summary>
    /// Registers many participants at once — the path generated registration uses.
    /// </summary>
    /// <param name="participantTypes">The handler and interceptor types to register.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Unlike <see cref="Register(Type)"/> this does not check the module: the generator has
    /// already sorted its discoveries by module, and not every participant contract carries
    /// the marker.
    /// </remarks>
    public QueryModuleBuilder RegisterParticipants(IEnumerable<Type> participantTypes)
    {
        _compositions.Select(participantTypes);
        return this;
    }
}
