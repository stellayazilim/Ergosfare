namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>The concrete closure of <see cref="MessageResultRoot"/>; instantiated by generated code.</summary>
public sealed class MessageResultRoot<TMessage, TResult> : MessageResultRoot where TMessage : IMessage
{
    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IMessageResultRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult>(state);
}
