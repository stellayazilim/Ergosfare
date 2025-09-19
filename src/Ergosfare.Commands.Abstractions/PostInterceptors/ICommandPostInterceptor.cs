using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a post-processing interceptor for commands that executes after any <see cref="ICommand"/> is handled.
/// </summary>
/// <remarks>
/// This interceptor is non-generic and non-type-safe. It can be registered to run for multiple command types
/// without specifying a particular result type. The <see cref="HandleAsync"/> method returns <see cref="object"/>,
/// so any result modifications are handled via object references and casting.
///
/// For scenarios where type safety is required, use the generic versions:
/// <see cref="ICommandPostInterceptor{TCommand,TResult}"/> or 
/// <see cref="ICommandPostInterceptor{TCommand,TResult,TModifiedResult}"/>.
/// </remarks>
public interface ICommandPostInterceptor: ICommand, IAsyncPostInterceptor<ICommand>;