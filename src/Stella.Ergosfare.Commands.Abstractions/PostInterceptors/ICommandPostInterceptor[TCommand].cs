using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;



/// <summary>
/// Represents a post-processing interceptor for commands. 
/// This non-generic, non-type-safe version returns <see cref="object"/> from the pipeline.
/// </summary>
/// <typeparam name="TCommand">The type of command this interceptor handles. Must implement <see cref="ICommand"/>.</typeparam>
/// <remarks>
/// Use this interface to register post-interceptors in the command pipeline without specifying a strongly-typed result. 
/// For type-safe scenarios, prefer using the generic version: 
/// <see cref="ICommandPostInterceptor{TCommand, TResult, TModifiedResult}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandPostInterceptor<in TCommand>: ICommand, IAsyncPostInterceptor<TCommand>  where TCommand : ICommand;