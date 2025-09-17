using Ergosfare.Core.Abstractions;

namespace Ergosfare.Test.Fixtures.Stubs.Generic;


/// <summary>
/// A generic stub message used for testing handlers and interceptors with generic message types.
/// </summary>
/// <typeparam name="T">The type parameter used to distinguish different generic message instances.</typeparam>
public record StubGenericMessage<T>: IMessage;

public record IndirectStubGenericMessage<TGenericType>: StubGenericMessage<TGenericType>;