namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>Generic re-entry point for consumers of <see cref="MessageRoot"/>.</summary>
public interface IMessageRootVisitor<out TReturn, in TState>
{
    /// <summary>Called with the root's message type as the generic argument.</summary>
    TReturn Visit<TMessage>(TState state) where TMessage : IMessage;
}
