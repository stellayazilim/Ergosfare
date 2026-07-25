using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Test.__stubs__;

/// <summary>
/// Represents a stub query that returns a <see cref="string"/> result,
/// used for testing non-generic query handling.
/// </summary>
public record StubNonGenericStringResultQuery: IQuery<string>;

/// <summary>
/// Represents a stub stream query that returns a stream of <see cref="string"/> results,
/// used for testing non-generic streaming query handling.
/// </summary>
public record StubNonGenericStreamStringResultQuery: IStreamQuery<string>;