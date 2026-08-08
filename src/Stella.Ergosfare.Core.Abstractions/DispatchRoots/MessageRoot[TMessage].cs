namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>The concrete closure of <see cref="MessageRoot"/>; instantiated by generated code.</summary>
public sealed class MessageRoot<TMessage> : MessageRoot where TMessage : IMessage
{
    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IMessageRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage>(state);
}
