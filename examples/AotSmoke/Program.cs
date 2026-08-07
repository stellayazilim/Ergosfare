// End-to-end NativeAOT smoke: source-generated registration + every dispatch shape the
// library advertises for AOT, executed in a PublishAot=true binary. Any failure exits
// non-zero, so CI treats the published binary's run as the assertion.

using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

var provider = new ServiceCollection()
    .AddErgosfare(options =>
    {
        options.AddCommandModule(commands => commands.RegisterGenerated());
        options.AddQueryModule(queries => queries.RegisterGenerated());
        options.AddEventModule(events => events.RegisterGenerated());
    })
    .BuildServiceProvider();

await using var _ = provider;

var failures = new List<string>();

// Void command — the generated void plan's devirtualized, directly-constructed path.
var commands = provider.GetRequiredService<ICommandMediator>();
var voidSettings = new CommandMediationSettings();
await commands.SendAsync(new Ergosfare.AotSmoke.CreateNote(), voidSettings);
if (!Equals(voidSettings.Items["noteCreated"], true))
{
    failures.Add("void command handler did not run");
}

// Result command — the generated result plan.
var echoed = await commands.SendAsync(new Ergosfare.AotSmoke.EchoNote { Text = "aot" });
if (echoed != "aot!")
{
    failures.Add($"result command returned '{echoed}', expected 'aot!'");
}

// Query — result executor over a value-type result.
var queries = provider.GetRequiredService<IQueryMediator>();
var answer = await queries.QueryAsync(new Ergosfare.AotSmoke.TheAnswer());
if (answer != 42)
{
    failures.Add($"query returned {answer}, expected 42");
}

// Class event broadcast with two handlers.
var events = provider.GetRequiredService<IEventMediator>();
var publishSettings = new EventMediationSettings();
await events.PublishAsync(new Ergosfare.AotSmoke.NotePublished(), publishSettings);
if (!Equals(publishSettings.Items["firstSubscriber"], true) || !Equals(publishSettings.Items["secondSubscriber"], true))
{
    failures.Add("event broadcast did not reach both handlers");
}

// Struct event — the value-type generic instantiations only generated roots anchor
// under AOT (shared generic code cannot cover them).
var structSettings = new EventMediationSettings();
await events.PublishAsync(new Ergosfare.AotSmoke.StructPing(7), structSettings);
if (!Equals(structSettings.Items["structPayload"], 7))
{
    failures.Add("struct event handler did not observe the payload");
}

if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"AOT smoke FAILED: {failure}");
    }

    return 1;
}

Console.WriteLine("AOT smoke passed: all dispatch shapes completed.");
return 0;

namespace Ergosfare.AotSmoke
{
    public sealed class CreateNote : ICommand { }

    public sealed class CreateNoteHandler : ICommandHandler<CreateNote>
    {
        public ValueTask HandleAsync(CreateNote command, IExecutionContext context)
        {
            context.Set("noteCreated", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class EchoNote : ICommand<string>
    {
        public string Text { get; init; } = string.Empty;
    }

    public sealed class EchoNoteHandler : ICommandHandler<EchoNote, string>
    {
        public ValueTask<string> HandleAsync(EchoNote command, IExecutionContext context)
            => ValueTask.FromResult(command.Text + "!");
    }

    public sealed class TheAnswer : IQuery<int> { }

    public sealed class TheAnswerHandler : IQueryHandler<TheAnswer, int>
    {
        public ValueTask<int> HandleAsync(TheAnswer query, IExecutionContext context)
            => ValueTask.FromResult(42);
    }

    public sealed class NotePublished : IEvent { }

    public sealed class FirstNoteSubscriber : IEventHandler<NotePublished>
    {
        public ValueTask HandleAsync(NotePublished @event, IExecutionContext context)
        {
            context.Set("firstSubscriber", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class SecondNoteSubscriber : IEventHandler<NotePublished>
    {
        public ValueTask HandleAsync(NotePublished @event, IExecutionContext context)
        {
            context.Set("secondSubscriber", true);
            return ValueTask.CompletedTask;
        }
    }

    public readonly struct StructPing(int payload) : IEvent
    {
        public int Payload { get; } = payload;
    }

    public sealed class StructPingHandler : IEventHandler<StructPing>
    {
        public ValueTask HandleAsync(StructPing @event, IExecutionContext context)
        {
            context.Set("structPayload", @event.Payload);
            return ValueTask.CompletedTask;
        }
    }
}
