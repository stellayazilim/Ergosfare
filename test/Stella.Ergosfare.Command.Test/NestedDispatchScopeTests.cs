using Stella.Ergosfare.Command.Test.__stubs__;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Nested dispatch through execution-context scopes: a handler opens a scope on its own
/// context and sends an inner command under the child. The child starts with clean items
/// (isolation) and inherits the parent's cancellation token; disposing the scope returns
/// the child to the pool.
/// </summary>
public class NestedDispatchScopeTests
{
    [ExcludeFromDiscovery]
    public sealed record OuterCommand : ICommand;

    [ExcludeFromDiscovery]
    public sealed record InnerCommand : ICommand<string>;

    public sealed class Recorder
    {
        public bool InnerSawOuterItem;
        public CancellationToken InnerToken;
        public string? InnerResult;
    }

    [ExcludeFromDiscovery]
    public sealed class OuterHandler(ICommandMediator commands, Recorder recorder) : ICommandHandler<OuterCommand>
    {
        public async ValueTask HandleAsync(OuterCommand message, IExecutionContext context)
        {
            context.Set("outer", "state");

            using var scope = context.CreateScope();

            recorder.InnerResult = await commands.SendAsync(new InnerCommand(), scope.Context);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class InnerHandler(Recorder recorder) : ICommandHandler<InnerCommand, string>
    {
        public ValueTask<string> HandleAsync(InnerCommand message, IExecutionContext context)
        {
            // Clean child: the outer context's items must not leak in; the outer token must.
            recorder.InnerSawOuterItem = context.Has("outer");
            recorder.InnerToken = context.CancellationToken;
            return new("inner-done");
        }
    }

    [Fact]
    public async Task HandlerScope_DispatchesInnerCommand_WithIsolationAndInheritedToken()
    {
        var services = new ServiceCollection()
            .AddSingleton<Recorder>()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<OuterCommand>()
                .Register<InnerCommand>()
                .Register(typeof(OuterHandler))
                .Register(typeof(InnerHandler))))
            .BuildServiceProvider();

        var recorder = services.GetRequiredService<Recorder>();
        using var cts = new CancellationTokenSource();

        await services.GetRequiredService<ICommandMediator>()
            .SendAsync(new OuterCommand(), cancellationToken: cts.Token);

        Assert.Equal("inner-done", recorder.InnerResult);
        Assert.False(recorder.InnerSawOuterItem);
        Assert.Equal(cts.Token, recorder.InnerToken);
    }
}
