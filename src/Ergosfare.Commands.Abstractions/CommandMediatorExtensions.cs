namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Provides extension methods for <see cref="ICommandMediator"/> to simplify sending commands.
/// </summary>
public static class CommandMediatorExtensions
{
    
    /// <summary>
    /// Sends a command asynchronously without specifying groups.
    /// </summary>
    /// <param name="commandMediator">The command mediator instance.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task SendAsync(this ICommandMediator commandMediator, ICommand command,
        CancellationToken cancellationToken = default)
    {
        return commandMediator.SendAsync(command,null, cancellationToken);
    }

    /// <summary>
    /// Sends a command that returns a result asynchronously without specifying groups.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the command.</typeparam>
    /// <param name="commandMediator">The command mediator instance.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation returning the result.</returns>
    public static Task<TResult> SendAsync<TResult>(this ICommandMediator commandMediator, ICommand<TResult> command,
        CancellationToken cancellationToken = default)
    {
        return commandMediator.SendAsync(command, null, cancellationToken);
    }

    /// <summary>
    /// Sends a command asynchronously with the specified groups for filtering.
    /// </summary>
    /// <param name="commandMediator">The command mediator instance.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">The groups used to filter command handlers.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task SendAsync(
        this ICommandMediator commandMediator,
        ICommand command,
        string[] groups,
        CancellationToken cancellationToken = default
    )
    {
        return commandMediator.SendAsync(command, new CommandMediationSettings
        {
            Filters = { Groups = groups }
        }, cancellationToken);
    }
    
    /// <summary>
    /// Sends a command that returns a result asynchronously with the specified groups for filtering.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the command.</typeparam>
    /// <param name="commandMediator">The command mediator instance.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">The groups used to filter command handlers.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation returning the result.</returns>
    public static Task<TResult> SendAsync<TResult>(
        this ICommandMediator commandMediator,
        ICommand<TResult> command,
        string[] groups,
        CancellationToken cancellationToken = default
    )
    {
        return commandMediator.SendAsync(command, new CommandMediationSettings
        {
            Filters = { Groups = groups }
        }, cancellationToken);
    }

}