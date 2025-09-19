using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a non-type-safe exception interceptor for commands.
/// </summary>
/// <remarks>
/// This interceptor can handle any <see cref="ICommand"/> without specifying a particular result type.
/// It returns <see cref="object"/> from the <c>HandleAsync</c> method, making it suitable for
/// scenarios where you want to apply exception handling logic across multiple command types
/// in a generic pipeline without caring about the exact result type.
/// 
/// For scenarios where type safety is required, use one of the generic versions:
/// <see cref="ICommandExceptionInterceptor{TCommand, TResult}"/> or
/// <see cref="ICommandExceptionInterceptor{TCommand, TResult, TModifiedResult}"/>.
/// </remarks>
public interface ICommandExceptionInterceptor: ICommand, IAsyncExceptionInterceptor<ICommand>;