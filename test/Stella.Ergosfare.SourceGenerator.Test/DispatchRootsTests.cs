using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// Dispatch-root emission: every concrete, closed, handler-free marker type gets its
/// dispatch generics rooted through <c>GeneratedDispatchRoots</c> — message roots plus
/// result/stream roots per closed marker contract — while handlers, abstract types,
/// interfaces and open generics stay off the root list (they never carry a runtime
/// message's type).
/// </summary>
public class DispatchRootsTests
{
    private const string Source = """
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Queries.Abstractions;
        using Stella.Ergosfare.Events.Abstractions;
        using System.Threading.Tasks;

        namespace TestApp
        {
            public sealed record VoidPing : ICommand;

            public sealed record TypedPing : ICommand<string>;

            public sealed record TimeQuery : IQuery<System.DateTimeOffset>;

            public sealed record NumberStream : IStreamQuery<int>;

            public readonly record struct MetricRecorded : IEvent;

            public abstract class CommandBase : ICommand;

            public interface IAuditedCommand : ICommand;

            public sealed record Wrapped<T>(T Value) : ICommand;

            public sealed class VoidPingHandler : ICommandHandler<VoidPing>
            {
                public ValueTask HandleAsync(VoidPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                    => default;
            }
        }
        """;

    [Fact]
    public void Emission_RootsDispatchableMessagesOnly()
    {
        var result = GeneratorTestHost.Run(Source);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;

        Assert.Contains("GeneratedDispatchRoots.AddMessage<global::TestApp.VoidPing>();", source);
        Assert.Contains("GeneratedDispatchRoots.AddMessage<global::TestApp.MetricRecorded>();", source);
        Assert.Contains("GeneratedDispatchRoots.AddResult<global::TestApp.TypedPing, string>();", source);
        Assert.Contains("GeneratedDispatchRoots.AddResult<global::TestApp.TimeQuery, global::System.DateTimeOffset>();", source);
        Assert.Contains("GeneratedDispatchRoots.AddStream<global::TestApp.NumberStream, int>();", source);

        // Handlers, abstract bases, interfaces and open generics never carry a runtime
        // message's type — no roots for them.
        Assert.DoesNotContain("AddMessage<global::TestApp.VoidPingHandler>", source);
        Assert.DoesNotContain("AddMessage<global::TestApp.CommandBase>", source);
        Assert.DoesNotContain("AddMessage<global::TestApp.IAuditedCommand>", source);
        Assert.DoesNotContain("AddMessage<global::TestApp.Wrapped<", source);
    }

    private const string HiddenSource = """
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Queries.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        using System.Collections.Generic;
        using System.Threading.Tasks;

        namespace TestApp
        {
            [ExcludeFromDiscovery]
            public sealed record HiddenTyped : ICommand<string>;

            [ExcludeFromDiscovery]
            public sealed class HiddenTypedHandler : ICommandHandler<HiddenTyped, string>
            {
                public ValueTask<string> HandleAsync(HiddenTyped message, ErgosfareContext context)
                    => ValueTask.FromResult("hidden");
            }

            [ExcludeFromDiscovery]
            public sealed record HiddenStream : IStreamQuery<int>;

            [ExcludeFromDiscovery]
            public sealed class HiddenStreamHandler : IStreamQueryHandler<HiddenStream, int>
            {
                public async IAsyncEnumerable<int> StreamAsync(HiddenStream query, ErgosfareContext context)
                {
                    await Task.Yield();
                    yield return 1;
                }
            }

            public static class Boot
            {
                public static void Configure(
                    Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder commands,
                    Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder queries)
                {
                    commands.Register<HiddenTypedHandler>();
                    queries.Register<HiddenStreamHandler>();
                }
            }
        }
        """;

    [Fact]
    public void AHiddenMessageARegistrationReaches_RootsItsResultContractsToo()
    {
        var result = GeneratorTestHost.Run(HiddenSource);

        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;

        // Rooting is not registering: a message hidden from bulk collection still gets the
        // generics a dispatch has to close, because hiding it does not stop it from being
        // dispatched. The three roots are three tables, and a message root alone leaves a
        // dispatch by result or by stream closing through MakeGenericType — which has no
        // answer under Native AOT, so the same dispatch works in development and fails at
        // publish.
        Assert.Contains("GeneratedDispatchRoots.AddMessage<global::TestApp.HiddenTyped>();", source);
        Assert.Contains("GeneratedDispatchRoots.AddResult<global::TestApp.HiddenTyped, string>();", source);
        Assert.Contains("GeneratedDispatchRoots.AddStream<global::TestApp.HiddenStream, int>();", source);
    }

    [Fact]
    public void ExecutingRegisterAll_PopulatesTheProcessWideRootStore()
    {
        var result = GeneratorTestHost.Run(Source, referenceModuleBuilders: false);

        Assert.Empty(result.CompilationErrors);

        // The test process shares the real abstractions assembly with the emitted one, so
        // executing the generated registration lands the roots in the same static store.
        _ = typeof(FrozenCompositionCatalog);

        using var stream = new MemoryStream();
        Assert.True(result.OutputCompilation.Emit(stream).Success);

        var assembly = Assembly.Load(stream.ToArray());
        var registrations = assembly.GetType("Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations", throwOnError: true)!;
        var catalog = new FrozenCompositionCatalog();

        registrations.GetMethod("RegisterAll", [typeof(FrozenCompositionCatalog)])!.Invoke(null, [catalog]);

        var voidPing = assembly.GetType("TestApp.VoidPing", throwOnError: true)!;
        var typedPing = assembly.GetType("TestApp.TypedPing", throwOnError: true)!;
        var numberStream = assembly.GetType("TestApp.NumberStream", throwOnError: true)!;

        Assert.NotNull(GeneratedDispatchRoots.FindMessage(voidPing));
        Assert.NotNull(GeneratedDispatchRoots.FindResult(typedPing, typeof(string)));
        Assert.NotNull(GeneratedDispatchRoots.FindStream(numberStream, typeof(int)));
        Assert.Null(GeneratedDispatchRoots.FindMessage(assembly.GetType("TestApp.VoidPingHandler", throwOnError: true)!));
    }
}
