using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Represents a query message in the system, which can be dispatched through the query module.
/// </summary>
/// <remarks>
/// <para>
/// Any type implementing <see cref="IQuery"/> is considered a query and can be registered
/// within the query module, allowing it to be handled by query handlers and interceptors.
/// </para>
/// <para>
/// Queries are typically read-only operations that return a result, and they participate
/// in the query mediation pipeline.
/// </para>
/// </remarks>
public interface IQuery: IMessage;