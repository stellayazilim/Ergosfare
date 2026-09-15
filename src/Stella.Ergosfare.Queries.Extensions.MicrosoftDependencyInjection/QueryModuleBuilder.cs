using Stella.Ergosfare.Core.Abstractions.Planning;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Selects which of the compiled query constructs this container runs.
/// </summary>
/// <param name="compositions">
/// The catalog this builder records the container's selection in.
/// </param>
/// <exception cref="ArgumentNullException"><paramref name="compositions"/> is <c>null</c>.</exception>
public sealed class QueryModuleBuilder(DispatchPlanCatalog compositions)
{
    private readonly DispatchPlanCatalog _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));

    /// <summary>Applies the compile-time default selection for this module.</summary>
    public QueryModuleBuilder AddGenerated() => AddGenerated("");

    /// <summary>Applies the compile-time selection for a constant discovery-key pattern.</summary>
    /// <param name="discoveryKeyPattern">An exact key or trailing-star prefix.</param>
    /// <returns>The same builder.</returns>
    public QueryModuleBuilder AddGenerated(string discoveryKeyPattern)
    {
        GeneratedPlanRegistry.ApplyGeneratedSelection(_compositions, 2, discoveryKeyPattern);
        return this;
    }

    /// <summary>
    /// Registers one query construct.
    /// </summary>
    /// <typeparam name="TQuery">
    /// The type to register: a query, or one of the handlers and interceptors that serve
    /// queries.
    /// </typeparam>
    /// <returns>The same builder, so calls can be chained.</returns>
    public QueryModuleBuilder Register<TQuery>() where TQuery : IQuery
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
    public QueryModuleBuilder Register(Type queryType)
    {
        ArgumentNullException.ThrowIfNull(queryType);

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
