using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a pre-interceptor for commands that is invoked before the command
/// enters the pipeline. Can be used to modify the command or perform preparatory actions.
/// </summary>
/// <remarks>
/// This is a non-generic, non-type-safe version of a pre-interceptor. It works with any
/// command implementing <see cref="ICommand"/>. For type-safe interception, consider
/// using <see cref="ICommandPreInterceptor{TCommand}"/> or 
/// <see cref="ICommandPreInterceptor{TCommand, TModifiedCommand}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandPreInterceptor: ICommand, IAsyncPreInterceptor<ICommand>;