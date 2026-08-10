using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Abort.Generated;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Abort;

/// <summary>
/// The post-abort contract under source-generated registration: the abort unwinds out of
/// the emitted staged-plan body, through its <c>catch</c>-when-not-aborted guard and into
/// its <c>finally</c>.
/// </summary>
/// <remarks>
/// The pattern-less overload is deliberate: plan eligibility requires default-discovery,
/// top-level, ungrouped participants, so a keyed selection would abort inside the
/// reflective strategy the other axis already covers.
/// </remarks>
public sealed class GeneratedRegistrationPostAbortTests : PostAbortSemanticsContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated())
                .AddQueryModule(queries => queries.RegisterGenerated()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override IAbortCommand NewCommand() => new AbortVoidCommand();

    /// <inheritdoc />
    protected override IAbortResultCommand NewResultCommand() => new AbortResultCommand();

    /// <inheritdoc />
    protected override IAbortValueQuery NewValueQuery() => new AbortValueQuery();
}
