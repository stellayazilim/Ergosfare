using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

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
                public ValueTask HandleAsync(CreatePing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
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
        Assert.Contains("public static void RegisterAll(global::Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenCompositionCatalog compositions)", source);
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

        // Messages are named one call each: once in the module-agnostic catalog surface
        // and once in exactly one builder extension.
        Assert.Equal(1, CountOccurrences(source, "compositions.Select(typeof(global::TestApp.CreatePing));"));
        Assert.Equal(1, CountOccurrences(source, "builder.Register(typeof(global::TestApp.CreatePing));"));
        Assert.Equal(1, CountOccurrences(source, "compositions.Select(typeof(global::TestApp.GetPong));"));
        Assert.Equal(1, CountOccurrences(source, "builder.Register(typeof(global::TestApp.GetPong));"));
        Assert.Equal(1, CountOccurrences(source, "compositions.Select(typeof(global::TestApp.PingCreated));"));
        Assert.Equal(1, CountOccurrences(source, "builder.Register(typeof(global::TestApp.PingCreated));"));

        // Participants go into the builder's batch rather than through Register, which
        // asserts module membership a participant contract need not carry.
        Assert.Equal(1, CountOccurrences(source, "participants.Add(typeof(global::TestApp.CreatePingHandler));"));
        Assert.Equal(1, CountOccurrences(source, "compositions.Select(typeof(global::TestApp.CreatePingHandler));"));
        Assert.DoesNotContain("builder.Register(typeof(global::TestApp.CreatePingHandler))", source);
        Assert.Contains("builder.RegisterParticipants(participants);", source);
    }

    [Fact]
    public void GenericMessages_AreNamedByDefinitionWhileParticipantsStayVerbatim()
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
                    public ValueTask HandleAsync(Wrapped<int> message, ErgosfareContext context) => default;
                }

                public sealed class WrappedIntPre : ICommandPreInterceptor<Wrapped<int>>
                {
                    public ValueTask<Wrapped<int>> HandleAsync(Wrapped<int> message, ErgosfareContext context) => new(message);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;

        // The open generic message is named by its definition — how the table keys it —
        // while the participants closing over one instantiation are named verbatim.
        Assert.Contains("compositions.Select(typeof(global::TestApp.Wrapped<>));", source);
        Assert.Contains("participants.Add(typeof(global::TestApp.WrappedIntHandler));", source);
        Assert.Contains("participants.Add(typeof(global::TestApp.WrappedIntPre));", source);
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
                    public ValueTask<TCommand> HandleAsync(TCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => new(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains("typeof(global::TestApp.AuditPreInterceptor<>)", result.GeneratedSource);

        // Emitted in unbound form — and bound by nothing, which this test used to assert
        // was fine by demanding no diagnostics. It is not fine: an interceptor taking its
        // message as a type parameter appears in no message's pipeline and never runs, so
        // the registration above is all there is. ERGO016 is that fact said out loud.
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO016");
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
        Assert.Equal("ERGO001", diagnostic.Id);
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
    public void NoRegistrableTypes_EmitsOnlyTheDispatchManifestMarker()
    {
        var result = GeneratorTestHost.Run("""
            namespace TestApp
            {
                public sealed class JustAClass;
            }
            """);

        // No registrations to emit — but the manifest marker must still be stamped, so a
        // composition root can tell "this assembly dispatches nothing" from "unknown".
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("DispatchManifestAttribute(1)", result.GeneratedSource);
        Assert.DoesNotContain("DispatchSiteAttribute(", result.GeneratedSource);
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
    public void GeneratedRegisterAll_ExecutesAgainstACatalog()
    {
        var result = GeneratorTestHost.Run(FullSurfaceSource);

        Assert.Empty(result.CompilationErrors);

        // Make sure the real abstractions assembly is loaded so the emitted assembly's
        // references bind to it by name.
        _ = typeof(FrozenCompositionCatalog);

        using var stream = new MemoryStream();
        var emitResult = result.OutputCompilation.Emit(stream);
        Assert.True(emitResult.Success);

        var assembly = Assembly.Load(stream.ToArray());
        var registrations = assembly.GetType("Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations", throwOnError: true)!;
        var catalog = new FrozenCompositionCatalog();

        registrations.GetMethod("RegisterAll", [typeof(FrozenCompositionCatalog)])!.Invoke(null, [catalog]);

        // Messages and participants alike land in the container's selection — registration
        // names constructs, and the compiled table decides what each one's pipeline is.
        Assert.Equal(
            [
                assembly.GetType("TestApp.CreatePing", throwOnError: true)!,
                assembly.GetType("TestApp.CreatePingHandler", throwOnError: true)!,
                assembly.GetType("TestApp.GetPong", throwOnError: true)!,
                assembly.GetType("TestApp.PingCreated", throwOnError: true)!,
            ],
            catalog.Selections.OrderBy(type => type.Name, StringComparer.Ordinal));
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
