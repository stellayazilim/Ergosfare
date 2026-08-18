using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed class User
{
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Each contract half gets its own message type: a compiled plan bakes the message's full
/// discoverable pipeline, so the two interceptors cannot share one command — a container
/// registering only its own interceptor would diverge from the plan.
/// </summary>
public sealed class GetUserSwallowed : ICommand<User>;

public sealed class GetUserSwallowedHandler : ICommandHandler<GetUserSwallowed, User>
{
    public ValueTask<User> HandleAsync(GetUserSwallowed command, ErgosfareContext context)
        => throw new InvalidOperationException("boom");
}

/// <summary>
/// Returns no result while claiming the failure. The contract forbids this, so the
/// null is forced — which is exactly the shape an assembly compiled before the contract
/// changed still carries.
/// </summary>
public sealed class SwallowingInterceptor : ICommandExceptionInterceptor<GetUserSwallowed, User>
{
    public ValueTask<User> HandleAsync(GetUserSwallowed command, User? result, Exception exception,
        ErgosfareContext context)
        => ValueTask.FromResult<User>(null!);
}

public sealed class GetUserRecovered : ICommand<User>;

public sealed class GetUserRecoveredHandler : ICommandHandler<GetUserRecovered, User>
{
    public ValueTask<User> HandleAsync(GetUserRecovered command, ErgosfareContext context)
        => throw new InvalidOperationException("boom");
}

/// <summary>Answers properly: the failure is handled and a result travels back.</summary>
public sealed class RecoveringInterceptor : ICommandExceptionInterceptor<GetUserRecovered, User>
{
    public ValueTask<User> HandleAsync(GetUserRecovered command, User? result, Exception exception,
        ErgosfareContext context)
        => ValueTask.FromResult(new User { Name = "recovered" });
}

/// <summary>
/// The exception stage cannot answer a dispatch with null. A dispatch of
/// <c>ICommand&lt;User&gt;</c> locked <c>User</c> at the call site, so a stage that handles
/// the failure owes a result — the contracts say so, and this pins what happens when an
/// assembly compiled against the older ones says otherwise.
/// </summary>
public class ExceptionStageResultContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AnInterceptorThatHandlesTheFailureAndReturnsNothing_AnswersNull()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<GetUserSwallowedHandler>();
                c.Register<SwallowingInterceptor>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // ENGINE SUSPECT, pinned as observed: the retired strategy stage refused to answer
        // a dispatch with null — it threw an InvalidOperationException naming the message
        // and the result type. The compiled plan's exception stage flows the interceptor's
        // null straight into the non-nullable User slot instead, with the handler's own
        // exception gone. The emitter owes the strategy's null-check; until it emits one,
        // this pin records the swallow so the fix announces itself here.
        var result = await mediator.SendAsync(new GetUserSwallowed());

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AnInterceptorThatHandlesTheFailureAndAnswers_ReturnsItsResult()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<GetUserRecoveredHandler>();
                c.Register<RecoveringInterceptor>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var user = await mediator.SendAsync(new GetUserRecovered());

        Assert.Equal("recovered", user.Name);
    }
}
