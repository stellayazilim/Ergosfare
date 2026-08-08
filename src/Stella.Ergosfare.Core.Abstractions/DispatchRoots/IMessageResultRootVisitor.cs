namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>Generic re-entry point for consumers of <see cref="MessageResultRoot"/>.</summary>
public interface IMessageResultRootVisitor<out TReturn, in TState>
{
    /// <summary>Called with the root's message and result types as the generic arguments.</summary>
    TReturn Visit<TMessage, TResult>(TState state) where TMessage : IMessage;
}
