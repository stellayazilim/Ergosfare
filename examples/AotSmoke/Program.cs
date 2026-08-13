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
// The execution context is the items channel now: a caller that wants to read back what
// the pipeline wrote constructs one and uses the context overload. A directly constructed
// context is never pooled, so what handlers wrote is still there once the call returns.
var commands = provider.GetRequiredService<ICommandMediator>();
var voidContext = new ErgosfareContext();
await commands.SendAsync(new Ergosfare.AotSmoke.CreateNote(), voidContext);
if (!voidContext.TryGet<bool>("noteCreated", out var noteCreated) || !noteCreated)
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
var publishContext = new ErgosfareContext();
await events.PublishAsync(new Ergosfare.AotSmoke.NotePublished(), publishContext);
if (!publishContext.TryGet<bool>("firstSubscriber", out var firstSubscriber) || !firstSubscriber
    || !publishContext.TryGet<bool>("secondSubscriber", out var secondSubscriber) || !secondSubscriber)
{
    failures.Add("event broadcast did not reach both handlers");
}

// Struct event — the value-type generic instantiations only generated roots anchor under
// AOT (shared generic code cannot cover them). This row deliberately takes the *typed*
// publish, which resolves its pipeline from a static-generic slot rather than a runtime-type
// lookup: that slot is the instantiation AOT has to have compiled. The typed publish has no
// context overload, so this handler reports through a static sink instead of context items.
await events.PublishAsync(new Ergosfare.AotSmoke.StructPing(7));
if (Ergosfare.AotSmoke.StructPingSink.Payload != 7)
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
        public ValueTask HandleAsync(CreateNote command, ErgosfareContext context)
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
        public ValueTask<string> HandleAsync(EchoNote command, ErgosfareContext context)
            => ValueTask.FromResult(command.Text + "!");
    }

    public sealed class TheAnswer : IQuery<int> { }

    public sealed class TheAnswerHandler : IQueryHandler<TheAnswer, int>
    {
        public ValueTask<int> HandleAsync(TheAnswer query, ErgosfareContext context)
            => ValueTask.FromResult(42);
    }

    public sealed class NotePublished : IEvent { }

    public sealed class FirstNoteSubscriber : IEventHandler<NotePublished>
    {
        public ValueTask HandleAsync(NotePublished @event, ErgosfareContext context)
        {
            context.Set("firstSubscriber", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class SecondNoteSubscriber : IEventHandler<NotePublished>
    {
        public ValueTask HandleAsync(NotePublished @event, ErgosfareContext context)
        {
            context.Set("secondSubscriber", true);
            return ValueTask.CompletedTask;
        }
    }

    public readonly struct StructPing(int payload) : IEvent
    {
        public int Payload { get; } = payload;
    }

    /// <summary>Where the struct event's handler reports, the typed publish having no
    /// context overload to read items back from.</summary>
    public static class StructPingSink
    {
        public static int Payload = -1;
    }

    public sealed class StructPingHandler : IEventHandler<StructPing>
    {
        public ValueTask HandleAsync(StructPing @event, ErgosfareContext context)
        {
            StructPingSink.Payload = @event.Payload;
            return ValueTask.CompletedTask;
        }
    }
}
