using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.Registry;

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
    public void ModuleMembership_TypesAppearInTheirModulesExtensionOnly()
    {
        var result = GeneratorTestHost.Run(FullSurfaceSource);
        var source = result.GeneratedSource;

        // Each construct appears once in RegisterAll and once in exactly one builder extension.
        Assert.Equal(2, CountOccurrences(source, "typeof(global::TestApp.CreatePing)"));
        Assert.Equal(2, CountOccurrences(source, "typeof(global::TestApp.GetPong)"));
        Assert.Equal(2, CountOccurrences(source, "typeof(global::TestApp.PingCreated)"));
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

                    public int Count => 0;

                    public System.Collections.Generic.IEnumerator<Stella.Ergosfare.Core.Abstractions.Registry.Descriptors.IMessageDescriptor> GetEnumerator()
                    {
                        yield break;
                    }

                    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

                    public void Register(System.Type type) => Registered.Add(type);

                    public void RegisterDescriptors(System.Collections.Generic.IEnumerable<Stella.Ergosfare.Core.Abstractions.Registry.Descriptors.IHandlerDescriptor> descriptors)
                    {
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

        var registered = (List<Type>)registry.GetType().GetProperty("Registered")!.GetValue(registry)!;

        Assert.Equal(
        [
            assembly.GetType("TestApp.CreatePing", throwOnError: true)!,
            assembly.GetType("TestApp.CreatePingHandler", throwOnError: true)!,
            assembly.GetType("TestApp.GetPong", throwOnError: true)!,
            assembly.GetType("TestApp.PingCreated", throwOnError: true)!,
        ], registered);
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
