using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

/// <summary>
/// A basic stub message used for testing message handlers.
/// </summary>
public record StubMessage : IMessage;

/// <summary>
/// An indirect message type, derived from <see cref="StubMessage"/>.
/// Can be used to test handler resolution for assignable types.
/// </summary>
public record StubIndirectMessage : StubMessage;


/// <summary>
/// An unrelated message type that does not share inheritance with <see cref="StubMessage"/>.
/// Used to test handler selection and filtering.
/// </summary>
public record StubUnrelatedMessage : IMessage;

