using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.__stubs__;
using Moq;

namespace Ergosfare.Test.__mocks__;

public static class HandlerMocks
{
    public static Mock<IAsyncHandler<TMessage>> MockHandler<TMessage>() where TMessage: notnull => new ();
    public static Mock<IAsyncHandler<TMessage, TResult>> MockHandler<TMessage, TResult>() where TMessage: notnull => new ();
    
    /// <summary>
    /// Generates mock generic handler for <see cref="StubHandlers.StubGenericHandler{TArg}"/>
    /// </summary>
    /// <typeparam name="TArg">
    ///     Generic type param for stub handler
    /// </typeparam>
    /// <returns>A new <see cref="Moq.Mock{T}"/> of <see cref="StubHandlers.StubGenericHandler{TArg}"/>.</returns>
    public static Mock<StubHandlers.StubGenericHandler<TArg>> MockGenericHandler<TArg>() where TArg: notnull => new ();
}