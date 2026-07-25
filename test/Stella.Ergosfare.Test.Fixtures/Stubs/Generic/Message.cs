using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Test.Fixtures.Stubs.Generic;


/// <summary>
/// A generic stub message used for testing handlers and interceptors with generic message types.
/// </summary>
/// <typeparam name="T">The type parameter used to distinguish different generic message instances.</typeparam>
public record StubGenericMessage<T>: IMessage;


/// <summary>
/// A generic message type derived from <see cref="StubGenericMessage{TGenericType}"/>.
/// Used to represent an indirect message in tests or mediation pipelines.
/// </summary>
/// <typeparam name="TGenericType">The type parameter for the generic message.</typeparam>
public record IndirectStubGenericMessage<TGenericType>: StubGenericMessage<TGenericType>;