using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.SourceGenerator.Test;

public class ErgosfareRegistrationGeneratorTests
{
    private const string FullSurfaceSource = """
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Queries.Abstractions;
        using Stella.Ergosfare.Events.Abstractions;
        using System.Threading.Tasks;

        namespace TestApp
        {
            public sealed record CreatePing : ICommand;

            public sealed class CreatePingHandler : ICommandHandler<CreatePing>
            {
                public ValueTask HandleAsync(CreatePing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                    => default;
            }

            public sealed record GetPong : IQuery<string>;

            public sealed class PingCreated : IEvent;
        }
        """;

    [Fact]
    public void FullSurface_EmitsRegisterAllAndBuilderExtensions()
    {
        var result = GeneratorTestHost.Run(FullSurfaceSource);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;
        Assert.Contains("internal static class ErgosfareGeneratedRegistrations", source);
        Assert.Contains("public static void RegisterAll(global::Stella.Ergosfare.Core.Abstractions.Registry.IMessageRegistry registry)", source);
        Assert.Contains("typeof(global::TestApp.CreatePing)", source);
        Assert.Contains("typeof(global::TestApp.CreatePingHandler)", source);
        Assert.Contains("typeof(global::TestApp.GetPong)", source);
        Assert.Contains("typeof(global::TestApp.PingCreated)", source);

        Assert.Contains("global::Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder RegisterGenerated(", source);
        Assert.Contains("global::Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder RegisterGenerated(", source);
        Assert.Contains("global::Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder RegisterGenerated(", source);
    }

    [Fact]
    public void ModuleMembership_TypesAppearInTheirModulesRegistrationOnly()
    {
        var result = GeneratorTestHost.Run(FullSurfaceSource);
        var source = result.GeneratedSource;

        // Plain messages register through the runtime fallback: once in RegisterAll and
        // once in exactly one builder extension.
        Assert.Equal(1, CountOccurrences(source, "registry.Register(typeof(global::TestApp.CreatePing));"));
        Assert.Equal(1, CountOccurrences(source, "builder.Register(typeof(global::TestApp.CreatePing));"));
        Assert.Equal(1, CountOccurrences(source, "registry.Register(typeof(global::TestApp.GetPong));"));
        Assert.Equal(1, CountOccurrences(source, "builder.Register(typeof(global::TestApp.GetPong));"));
        Assert.Equal(1, CountOccurrences(source, "registry.Register(typeof(global::TestApp.PingCreated));"));
        Assert.Equal(1, CountOccurrences(source, "builder.Register(typeof(global::TestApp.PingCreated));"));

        // The handler registers through a pre-computed descriptor: once in the assembly-wide
        // factory and once in its module's factory — never through the runtime fallback.
        const string handlerDescriptor =
            "global::Stella.Ergosfare.Core.Abstractions.Registry.Descriptors.HandlerDescriptors.Handler(" +
            "typeof(global::TestApp.CreatePing), typeof(global::System.Threading.Tasks.ValueTask), typeof(global::TestApp.CreatePingHandler))";
        Assert.Equal(2, CountOccurrences(source, handlerDescriptor));
        Assert.DoesNotContain("Register(typeof(global::TestApp.CreatePingHandler))", source);
    }

    [Fact]
    public void InterceptorContracts_EmitDescriptorsMirroringRuntimeBuilders()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                [Weight(5)]
                [Group("audit", "ops")]
                public sealed class AuditPre : ICommandPreInterceptor<Ping>
                {
                    public ValueTask<object> HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => new(message);
                }

                public sealed class LogPost : ICommandPostInterceptor<Ping>
                {
                    public ValueTask<object> HandleAsync(Ping message, object messageResult, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => new(messageResult);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;

        // Weight and groups flow into the descriptor entry as trailing arguments.
        Assert.Contains(
            ".PreInterceptor(typeof(global::TestApp.Ping), typeof(global::TestApp.AuditPre), 5u, new string[] { \"audit\", \"ops\" })",
            source);

        // The result-agnostic post contract maps to a ResultType of object.
        Assert.Contains(
            ".PostInterceptor(typeof(global::TestApp.Ping), typeof(object), typeof(global::TestApp.LogPost))",
            source);
    }

    [Fact]
    public void GenericMessages_AreNormalizedForInterceptorsAndKeptVerbatimForHandlers()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Handlers;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record Wrapped<T>(T Value) : ICommand;

                public sealed class WrappedIntHandler : ICommandHandler<Wrapped<int>>
                {
                    public ValueTask HandleAsync(Wrapped<int> message, IExecutionContext context) => default;
                }

                public sealed class WrappedIntPre : ICommandPreInterceptor<Wrapped<int>>
                {
                    public ValueTask<object> HandleAsync(Wrapped<int> message, IExecutionContext context) => new(message);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;

        // Main handlers keep the declared message type verbatim; interceptors normalize to
        // the generic definition — exactly what the runtime builders produce.
        Assert.Contains(".Handler(typeof(global::TestApp.Wrapped<int>), typeof(global::System.Threading.Tasks.ValueTask), typeof(global::TestApp.WrappedIntHandler))", source);
        Assert.Contains(".PreInterceptor(typeof(global::TestApp.Wrapped<>), typeof(global::TestApp.WrappedIntPre))", source);

        // The open generic message itself falls back to runtime registration.
        Assert.Contains("registry.Register(typeof(global::TestApp.Wrapped<>));", source);
    }

    [Fact]
    public void MarkerInterfacesAndAbstractBases_AreRegisteredLikeRuntimeScanning()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public interface IAuditedCommand : ICommand;

                public abstract class CommandBase : ICommand;
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("typeof(global::TestApp.IAuditedCommand)", result.GeneratedSource);
        Assert.Contains("typeof(global::TestApp.CommandBase)", result.GeneratedSource);
    }

    [Fact]
    public void OpenGenericHandler_IsEmittedInUnboundForm()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed class AuditPreInterceptor<TCommand> : ICommandPreInterceptor<TCommand>
                    where TCommand : ICommand
                {
                    public ValueTask<object> HandleAsync(TCommand message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => new(message);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("typeof(global::TestApp.AuditPreInterceptor<>)", result.GeneratedSource);
    }

    [Fact]
    public void NestedTypes_PublicIsRegistered_PrivateIsSkippedWithWarning()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public static class Container
                {
                    public sealed record VisibleCommand : ICommand;

                    private sealed record HiddenCommand : ICommand;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains("typeof(global::TestApp.Container.VisibleCommand)", result.GeneratedSource);
        Assert.DoesNotContain("HiddenCommand", result.GeneratedSource);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG001", diagnostic.Id);
        Assert.Contains("HiddenCommand", diagnostic.GetMessage());
    }

    [Fact]
    public void WithoutModuleBuilderReferences_OnlyRegisterAllIsEmitted()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record CreatePing : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("RegisterAll", result.GeneratedSource);
        Assert.DoesNotContain("RegisterGenerated", result.GeneratedSource);
    }

    [Fact]
    public void NoRegistrableTypes_EmitsNothing()
    {
        var result = GeneratorTestHost.Run("""
            namespace TestApp
            {
                public sealed class JustAClass;
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.DriverResult.GeneratedTrees);
    }

    [Fact]
    public void StructAndRecordStructMessages_AreRegistered()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Events.Abstractions;

            namespace TestApp
            {
                public readonly record struct MetricRecorded : IEvent;

                public struct PlainStructEvent : IEvent;
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("typeof(global::TestApp.MetricRecorded)", result.GeneratedSource);
        Assert.Contains("typeof(global::TestApp.PlainStructEvent)", result.GeneratedSource);
    }

    [Fact]
    public void PartialTypeWithMarkerOnBothParts_IsRegisteredOnce()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed partial record SplitCommand : ICommand;

                public sealed partial record SplitCommand : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        Assert.Empty(result.CompilationErrors);
        Assert.Equal(1, CountOccurrences(result.GeneratedSource, "typeof(global::TestApp.SplitCommand)"));
    }

    [Fact]
    public void GeneratedRegisterAll_ExecutesAgainstARegistry()
    {
        var result = GeneratorTestHost.Run(FullSurfaceSource + """

            namespace TestApp
            {
                public sealed class RecordingRegistry : Stella.Ergosfare.Core.Abstractions.Registry.IMessageRegistry
                {
                    public System.Collections.Generic.List<System.Type> Registered { get; } = new();

                    public System.Collections.Generic.List<Stella.Ergosfare.Core.Abstractions.Registry.Descriptors.IHandlerDescriptor> Descriptors { get; } = new();

                    public int Count => 0;

                    public System.Collections.Generic.IEnumerator<Stella.Ergosfare.Core.Abstractions.Registry.Descriptors.IMessageDescriptor> GetEnumerator()
                    {
                        yield break;
                    }

                    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

                    public void Register(System.Type type) => Registered.Add(type);

                    public void RegisterDescriptors(System.Collections.Generic.IEnumerable<Stella.Ergosfare.Core.Abstractions.Registry.Descriptors.IHandlerDescriptor> descriptors)
                    {
                        Descriptors.AddRange(descriptors);
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // Make sure the real abstractions assembly is loaded so the emitted assembly's
        // references bind to it by name.
        _ = typeof(IMessageRegistry);

        using var stream = new MemoryStream();
        var emitResult = result.OutputCompilation.Emit(stream);
        Assert.True(emitResult.Success);

        var assembly = Assembly.Load(stream.ToArray());
        var registrations = assembly.GetType("Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations", throwOnError: true)!;
        var registry = Activator.CreateInstance(assembly.GetType("TestApp.RecordingRegistry", throwOnError: true)!)!;

        registrations.GetMethod("RegisterAll")!.Invoke(null, [registry]);

        // Plain messages go through the runtime fallback...
        var registered = (List<Type>)registry.GetType().GetProperty("Registered")!.GetValue(registry)!;

        Assert.Equal(
        [
            assembly.GetType("TestApp.CreatePing", throwOnError: true)!,
            assembly.GetType("TestApp.GetPong", throwOnError: true)!,
            assembly.GetType("TestApp.PingCreated", throwOnError: true)!,
        ], registered);

        // ...while the handler arrives as a fully pre-computed descriptor.
        var descriptors = (System.Collections.IList)registry.GetType().GetProperty("Descriptors")!.GetValue(registry)!;
        var descriptor = Assert.IsAssignableFrom<IMainHandlerDescriptor>(Assert.Single(descriptors.Cast<object>()));

        Assert.Equal(assembly.GetType("TestApp.CreatePingHandler", throwOnError: true), descriptor.HandlerType);
        Assert.Equal(assembly.GetType("TestApp.CreatePing", throwOnError: true), descriptor.MessageType);
        Assert.Equal(typeof(ValueTask), descriptor.ResultType);
        Assert.Equal(0u, descriptor.Weight);
        Assert.Equal(["default"], descriptor.Groups);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;

        for (var index = text.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
