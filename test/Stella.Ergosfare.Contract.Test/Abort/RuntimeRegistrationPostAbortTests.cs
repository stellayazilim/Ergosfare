using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Abort.Fallback;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Abort;

/// <summary>
/// The same post-abort contract under explicit runtime registration: the abort unwinds out
/// of the reflective mediation strategy instead of an emitted plan.
/// </summary>
/// <remarks>
/// Each stage's slots are registered out of weight order on purpose: if registration order
/// decided which post interceptor aborts first, the inherited expectations would fail here
/// and pass on the generated axis.
/// </remarks>
public sealed class RuntimeRegistrationPostAbortTests : PostAbortSemanticsContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<AbortVoidCommandHandler>()
                    .Register<AbortVoidCommandLatePost>()
                    .Register<AbortVoidCommandPost>()
                    .Register<AbortVoidCommandException>()
                    .Register<AbortVoidCommandFinal>()
                    .Register<AbortResultCommandHandler>()
                    .Register<AbortResultCommandLatePost>()
                    .Register<AbortResultCommandPost>()
                    .Register<AbortResultCommandException>()
                    .Register<AbortResultCommandFinal>())
                .AddQueryModule(queries => queries
                    .Register<AbortValueQueryHandler>()
                    .Register<AbortValueQueryLatePost>()
                    .Register<AbortValueQueryPost>()
                    .Register<AbortValueQueryException>()
                    .Register<AbortValueQueryFinal>()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override IAbortCommand NewCommand() => new AbortVoidCommand();

    /// <inheritdoc />
    protected override IAbortResultCommand NewResultCommand() => new AbortResultCommand();

    /// <inheritdoc />
    protected override IAbortValueQuery NewValueQuery() => new AbortValueQuery();
}
