using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

// A dispatch nothing in the compiled closure can serve. The generator judges this at the
// composition root and fails the build — the whole point of the fixture, so there is no
// suppression here and no handler anywhere in the project.
var services = new ServiceCollection()
    .AddErgosfare(ergosfare => ergosfare.AddCommandModule(_ => { }))
    .BuildServiceProvider();

await services.GetRequiredService<ICommandMediator>().SendAsync(new Orphan());

/// <summary>A command with no handler, anywhere.</summary>
public sealed record Orphan : ICommand;
