namespace Ergosfare.Test.__stubs__;


public static class StubMessages
{
    public record StubNonGenericMessage : IMessage;
    public record StubNonGenericMessage2 : IMessage;
    public record StubNonGenericDerivedMessage : StubNonGenericMessage;
    public record StubNonGenericCommand : ICommand<Task>;
   
    

    /// <summary>
    /// Generic message stub for use in test cases
    /// </summary>
    /// <typeparam name="TArg"> Stub generic argument </typeparam>
    public record StubGenericMessage<TArg> : IMessage;
    public record StubGenericDerivedMessage<TArg> : StubGenericMessage<TArg>;
}
