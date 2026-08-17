namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
/// Marks a type as a command whose handler returns a <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type the handler returns.</typeparam>
/// <remarks>
/// Use this where the caller needs something back from the operation — a generated
/// identifier, a computed value, an outcome to act on. A command that only changes state
/// implements <see cref="ICommand"/> instead.
/// </remarks>
public interface ICommand<TResult> : ICommand;
