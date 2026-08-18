
namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// Re-enters a generic context carrying a staged stream plan's query and item types; the
/// streaming counterpart of <see cref="IStagedResultPlanVisitor{TReturn, TState}"/>.
/// </summary>
/// <typeparam name="TReturn">What the visitor produces.</typeparam>
/// <typeparam name="TState">The state the visitor needs.</typeparam>
public interface IStagedStreamPlanVisitor<out TReturn, in TState>
{
    /// <summary>
    /// Called inside the generic context the plan carries.
    /// </summary>
    /// <typeparam name="TQuery">The plan's query type.</typeparam>
    /// <typeparam name="TResult">The plan's item type.</typeparam>
    /// <param name="state">The state the caller passed.</param>
    /// <returns>Whatever the caller wants built.</returns>
    TReturn Visit<TQuery, TResult>(TState state) where TQuery : notnull;
}
