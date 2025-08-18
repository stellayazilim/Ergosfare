namespace Ergosfare.Test.__stubs__;


public static class StubMessages
{
    public record StubNonGenericMessage : IMessage;
    public record StubNonGenericCommand : ICommand<Task>;
    public record StubNonGenericDerivedMessage : StubNonGenericMessage;
    
    
    // ReSharper disable once ClassNeverInstantiated.Global
    public record StubNonGenericMessage2 : IMessage;

    /// <summary>
    /// Generic message stub for use in test cases
    /// </summary>
    /// <typeparam name="TArg"> Stub generic argument </typeparam>
    public record StubGenericMessage<TArg> : IMessage;
}
