# Writing an Ergosfare plugin

A plugin is a NuGet package that declares what it wants to observe. The generator writes the
call into the consumer's dispatch plans at compile time, so an installed plugin costs a
direct call and an uninstalled one costs nothing at all — there is no hook list to walk and
no runtime check to fail.

This example is a complete one. It times every dispatch and logs the result. The consumer
side is `examples/e2e`, which installs it — so the plugin also crosses that app's NativeAOT
publish, which is what actually tests the claim that baked hook calls survive trimming.

> The plugin surface is experimental behind `ERGOEXP002`. Both this project and the app
> installing it opt in with `<NoWarn>$(NoWarn);ERGOEXP002</NoWarn>`.

## 0. What a plugin project references

Three things, and one of them is not obvious:

- **`Stella.Ergosfare.Plugins.Abstractions`** — the declaration surface. It references nothing
  itself, which is what lets a new hook ship without moving the core's version.
- **`Stella.Ergosfare.Core.Abstractions`** — for `ErgosfareContext`, which hook methods take.
- **`Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection`** — for the `IModule` and
  `IModuleRegistry` the generated facade implements and extends. A plugin cannot avoid this
  one: without it the generator emits no facade, and the plugin has no way to be installed.

Plus the generator itself as an analyzer — it is what writes the facade.

## 1. Declare the assembly a plugin

One attribute. The generator answers it by writing an `IModule` and an `AddTiming()`
extension into this compilation — that extension is the entire install surface a consumer
sees.

```csharp
[assembly: ErgosfarePlugin("Timing")]
```

Name the assembly outside the reserved `Stella.Ergosfare.*` prefix. A consumer's reference
scan skips library assemblies, so a plugin named under it would never be read. `ERGOSG015`
reports that rather than letting it pass silently.

## 2. Write the hooks

```csharp
public sealed class TimingHooks
{
    private const string StartedAt = "timing.startedAt";

    [PipelineInvokable(Hook.Start)]
    public void Started<TMessage>(TMessage message, ErgosfareContext context)
        => context.Set(StartedAt, Stopwatch.GetTimestamp());

    [PipelineInvokable(Hook.Finish)]
    public void Finished<TMessage>(TMessage message, ErgosfareContext context, ILogger<TimingHooks> logger)
    {
        if (!context.TryGet<long>(StartedAt, out var startedAt))
        {
            return;
        }

        logger.LogInformation(
            "{Message} took {Elapsed:0.000} ms",
            typeof(TMessage).Name,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }
}
```

Four things are doing work here.

**`Hook` has four members**, and every one of them is a point that exists in every pipeline:
`Start`, `PreMain`, `PostMain`, `Finish`. All four are straight-line positions, so installing
a plugin never makes a plan grow a `try`, a `catch` or a `finally`. The interceptor stages are
deliberately not addressable — a plugin observes the pipeline, not its composition. Anything
the four cannot see (the failure path, the produced result, a point that runs on every exit)
is an interceptor's job, and a plugin package ships interceptors just as easily.

**The method is generic over the message.** The generator closes it over each plan's concrete
type, so a value-typed message is never boxed on the way in. The constraint doubles as a
filter: `where TMessage : ICacheableQuery` is emitted only into plans whose message satisfies
it, and costs nothing anywhere else because nothing is emitted there.

**The service is a singleton and that is not configurable.** So no per-dispatch state on the
service — every dispatch in flight shares the instance, and two hooks are two separate
resolutions anyway. State that travels between hooks goes in `ErgosfareContext.Items`, which
is per dispatch. That is what `StartedAt` is doing.

**`ILogger<>` is not a parameter the generator recognizes**, so it is resolved from the
dispatching provider at the call site. That is how a scoped dependency reaches a singleton
hook without the service capturing one.

## 3. Install it

Neither the module nor the extension below is hand-written: the generator emits both into the
**plugin's own** compilation, from the assembly attribute in step 1.

```csharp
internal sealed class TimingModule : IModule
{
    public void Build(IModuleConfiguration configuration)
        => configuration.Services.TryAddSingleton<TimingHooks>();
}

[Experimental("ERGOEXP002")]
public static class TimingPluginModuleRegistryExtensions
{
    public static IModuleRegistry AddTiming(this IModuleRegistry moduleRegistry)
    {
        moduleRegistry.Register(new TimingModule());
        return moduleRegistry;
    }
}
```

So a consumer installs the plugin with one reference and one line:

```csharp
services.AddErgosfare(options =>
{
    options.AddCommandModule(commands => commands.RegisterGenerated());
    options.AddTiming();
});
```

`AddTiming()` registers the service. The calls are already in the plan.

## Settings, when a plugin needs them

This plugin takes none, and that stays the simplest case — `AddTiming()` is parameterless.
A plugin that does need settings names the type in its declaration:

```csharp
[assembly: ErgosfarePlugin("Timing", typeof(TimingOptions))]
```

`TimingOptions` is an ordinary class the author writes. The generator neither writes it nor
requires anything of it; it only makes `AddTiming` take one:

```csharp
o.AddTiming(new TimingOptions { Threshold = TimeSpan.FromMilliseconds(50) })
```

**The instance never enters the container.** The module holds what the consumer passed and
hands it straight to the constructed service, so nothing is registered for a type the DI
container has no reason to know about — and nothing resolves it per dispatch. Two ways to
receive it, and the author picks by what they write:

| The service writes | What the generator does |
|---|---|
| A constructor taking `TimingOptions` | Nothing to it. Constructs it with the options, and resolves the constructor's *other* parameters from the container — so a plugin service takes ordinary dependencies too |
| No constructor, class marked `partial` | Writes the other half: a `private readonly TimingOptions _options;` and the one line that assigns it |
| No constructor, not `partial` | Nothing reaches it, and `ERGOSG017` says so rather than letting the settings be silently ignored |

## What the consumer's plan looks like

For a command with one handler and no interceptors, that is the whole plan:

```csharp
public override async ValueTask ExecuteDirect(
    SlowGreeting message, ErgosfareContext context, IServiceProvider serviceProvider)
{
    GetRequiredService<TimingHooks>(serviceProvider).Started<SlowGreeting>(message, context);
    await new SlowGreetingHandler().HandleAsync(message, context);
    GetRequiredService<TimingHooks>(serviceProvider).Finished<SlowGreeting>(
        message, context, GetRequiredService<ILogger<TimingHooks>>(serviceProvider));
}
```

(Fully qualified names elided for reading.) A straight line: hook, handler, hook. No
try/catch, no finally, no indirection — and with the plugin reference removed, the same
pipeline compiles down to the handler call alone.

## Seeing it for yourself

```bash
dotnet build examples/TimingPlugin -p:EmitCompilerGeneratedFiles=true
```

The facade lands in `obj/…/generated/…/ErgosfarePluginFacade.g.cs`. The same switch on a
consuming project shows the plan bodies the calls were baked into.
