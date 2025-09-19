namespace Ergosfare.Commands.Abstractions;

public static class CommandMediatorExtensions
{
    public static Task SendAsync(this ICommandMediator commandMediator, ICommand command,
        CancellationToken cancellationToken = default)
    {
        return commandMediator.SendAsync(command,null, cancellationToken);
    }



    public static Task<TResult> SendAsync<TResult>(this ICommandMediator commandMediator, ICommand<TResult> command,
        CancellationToken cancellationToken = default)
    {
        return commandMediator.SendAsync(command, null, cancellationToken);
    }


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