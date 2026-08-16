using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// The exception stage cannot answer a dispatch with null. A dispatch of
/// <c>ICommand&lt;User&gt;</c> locked <c>User</c> at the call site, so a stage that handles
/// the failure owes a result — the contracts say so, and this pins what happens when an
/// assembly compiled against the older ones says otherwise.
/// </summary>
public class ExceptionStageResultContractTests
{
    public sealed class User
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class GetUser : ICommand<User>;

    // ERGO007 (suppressed in the csproj): reached through the hand-registered module below.
    public sealed class GetUserHandler : ICommandHandler<GetUser, User>
    {
        public ValueTask<User> HandleAsync(GetUser command, ErgosfareContext context)
            => throw new InvalidOperationException("boom");
    }

    /// <summary>
    /// Returns no result while claiming the failure. The contract forbids this, so the
    /// null is forced — which is exactly the shape an assembly compiled before the contract
    /// changed still carries.
    /// </summary>
    public sealed class SwallowingInterceptor : ICommandExceptionInterceptor<GetUser, User>
    {
        public ValueTask<User> HandleAsync(GetUser command, User? result, Exception exception,
            ErgosfareContext context)
            => ValueTask.FromResult<User>(null!);
    }

    /// <summary>Answers properly: the failure is handled and a result travels back.</summary>
    public sealed class RecoveringInterceptor : ICommandExceptionInterceptor<GetUser, User>
    {
        public ValueTask<User> HandleAsync(GetUser command, User? result, Exception exception,
            ErgosfareContext context)
            => ValueTask.FromResult(new User { Name = "recovered" });
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AnInterceptorThatHandlesTheFailureAndReturnsNothing_Throws()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<GetUserHandler>();
                c.Register<SwallowingInterceptor>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // It used to answer null here — into a non-nullable slot, with the handler's
        // exception gone and nothing said about either.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mediator.SendAsync(new GetUser()));

        Assert.Contains(nameof(GetUser), thrown.Message);
        Assert.Contains(nameof(User), thrown.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AnInterceptorThatHandlesTheFailureAndAnswers_ReturnsItsResult()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<GetUserHandler>();
                c.Register<RecoveringInterceptor>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var user = await mediator.SendAsync(new GetUser());

        Assert.Equal("recovered", user.Name);
    }
}
