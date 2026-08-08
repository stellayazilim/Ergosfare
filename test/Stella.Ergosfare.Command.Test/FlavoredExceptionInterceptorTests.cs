using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Regression coverage for the flavored exception interceptor contract: it used to extend
/// the result-typed <c>IAsyncExceptionInterceptor&lt;TCommand, object&gt;</c>, which no
/// invocation-strategy arm can match on a void pipeline (its internal
/// <see cref="ValueTask"/> result carrier is a value type — no variance), so the exception
/// stage itself threw <see cref="NotSupportedException"/> and buried the handler's
/// exception. Re-based on the result-agnostic contract, the interceptor observes the
/// exception and the strategy swallows it — the documented contract.
/// </summary>
public class FlavoredExceptionInterceptorTests
{
    [ExcludeFromDiscovery]
    public sealed class VoidFailingCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class VoidFailingCommandHandler : ICommandHandler<VoidFailingCommand>
    {
        public ValueTask HandleAsync(VoidFailingCommand command, IExecutionContext context)
            => throw new InvalidOperationException("boom");
    }

    [ExcludeFromDiscovery]
    public sealed class VoidFailingCommandExceptionInterceptor : ICommandExceptionInterceptor<VoidFailingCommand>
    {
        public ValueTask<object> HandleAsync(VoidFailingCommand command, object? messageResult, Exception exception, IExecutionContext context)
        {
            context.Set("observed", exception.Message);
            return ValueTask.FromResult(messageResult!);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task VoidCommand_ExceptionInterceptorObservesAndSwallows()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<VoidFailingCommandHandler>();
                c.Register<VoidFailingCommandExceptionInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var settings = new CommandMediationSettings();

        // Before the contract fix this dispatch failed with NotSupportedException from
        // the exception invoker itself; now the interceptor observes the handler's
        // exception and the pipeline swallows it.
        await provider.GetRequiredService<ICommandMediator>().SendAsync(new VoidFailingCommand(), settings);

        Assert.Equal("boom", settings.Items["observed"]);
    }
}
