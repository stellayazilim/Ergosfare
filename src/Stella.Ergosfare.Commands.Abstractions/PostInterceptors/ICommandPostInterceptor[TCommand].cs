using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;



/// <summary>
/// Runs after the handler of a <typeparamref name="TCommand"/>, without naming the result
/// type.
/// </summary>
/// <typeparam name="TCommand">The command type this interceptor accepts.</typeparam>
/// <remarks>
/// Use this where the work applies to any result — logging or metrics, say — and the result
/// arrives as <see cref="object"/>. To read or replace a typed result, implement
/// <see cref="ICommandPostInterceptor{TCommand, TResult}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandPostInterceptor<in TCommand>: ICommand, IAsyncPostInterceptor<TCommand>  where TCommand : ICommand;
