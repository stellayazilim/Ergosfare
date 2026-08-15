namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Dispatch-site discovery and manifest emission: every mediator dispatch invocation
///     in the compilation's own source is recorded as an assembly-level
///     <c>DispatchSite</c> attribute — casts looked through, opacity marked — under the
///     always-present <c>DispatchManifest</c> marker. Emission is judgment-free: it runs
///     in every compilation, composition root or not.
/// </summary>
public class DispatchManifestEmissionTests
{
    private const string DispatchSitePrefix =
        "[assembly: global::Stella.Ergosfare.Core.Abstractions.DispatchSites.DispatchSiteAttribute(";

    private const string KindPrefix = "global::Stella.Ergosfare.Core.Abstractions.DispatchSites.DispatchKind.";

    [Fact]
    public void ConcreteCommandDispatch_IsRecordedWithItsStaticType()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                public sealed class PingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(Ping ping) => mediator.SendAsync(ping);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("DispatchManifestAttribute(1)", result.GeneratedSource);
        Assert.Contains(
            DispatchSitePrefix + "\"TestApp.Ping\", " + KindPrefix + "Command, false)]",
            result.GeneratedSource);
    }

    [Fact]
    public void CastToTheMarker_IsLookedThrough()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record MagnetarCommand : ICommand;

                public sealed class MagnetarHandler : ICommandHandler<MagnetarCommand>
                {
                    public ValueTask HandleAsync(MagnetarCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(MagnetarCommand x) => mediator.SendAsync((ICommand)x);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The cast target proves nothing; the operand's declared type is the evidence.
        Assert.Contains(
            DispatchSitePrefix + "\"TestApp.MagnetarCommand\", " + KindPrefix + "Command, false)]",
            result.GeneratedSource);
        Assert.DoesNotContain("\"Stella.Ergosfare.Commands.Abstractions.ICommand\"", result.GeneratedSource);
    }

    [Fact]
    public void MarkerTypedDispatch_IsRecordedAsOpaque()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                public sealed class PingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(ICommand whatever) => mediator.SendAsync(whatever);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains(
            DispatchSitePrefix + "\"Stella.Ergosfare.Commands.Abstractions.ICommand\", " + KindPrefix + "Command, true)]",
            result.GeneratedSource);
    }

    [Fact]
    public void QueryStreamAndEventDispatches_RecordTheirSurfaces()
    {
        var result = GeneratorTestHost.Run("""
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Events.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record TotalQuery : IQuery<int>;

                public sealed record RowStream : IStreamQuery<int>;

                public sealed record Blinked : IEvent;

                public sealed class TotalHandler : IQueryHandler<TotalQuery, int>
                {
                    public ValueTask<int> HandleAsync(TotalQuery message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => new(1);
                }

                public sealed class RowStreamHandler : IStreamQueryHandler<RowStream, int>
                {
                    public async IAsyncEnumerable<int> StreamAsync(RowStream message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                    {
                        await Task.Yield();
                        yield return 1;
                    }
                }

                public class Caller(IQueryMediator queries, IEventMediator events)
                {
                    public async Task Fire(TotalQuery q, RowStream s, Blinked e)
                    {
                        await queries.QueryAsync(q);
                        await foreach (var _ in queries.StreamAsync(s)) { }
                        await events.PublishAsync(e);
                    }
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains(
            DispatchSitePrefix + "\"TestApp.TotalQuery\", " + KindPrefix + "Query, false)]",
            result.GeneratedSource);
        Assert.Contains(
            DispatchSitePrefix + "\"TestApp.RowStream\", " + KindPrefix + "Stream, false)]",
            result.GeneratedSource);
        Assert.Contains(
            DispatchSitePrefix + "\"TestApp.Blinked\", " + KindPrefix + "Event, false)]",
            result.GeneratedSource);
    }

    [Fact]
    public void CoreMediatorDispatch_RecordsTheMessageSurface()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                public sealed class PingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, ErgosfareContext context)
                        => default;
                }

                public class Caller(IMessageMediator mediator)
                {
                    public ValueTask Fire(Ping ping) => mediator.DispatchAsync(ping);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains(
            DispatchSitePrefix + "\"TestApp.Ping\", " + KindPrefix + "Message, false)]",
            result.GeneratedSource);
    }

    [Fact]
    public void RepeatedDispatchesOfTheSameType_AreRecordedOnce()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                public sealed class PingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public async Task Fire(Ping a, Ping b)
                    {
                        await mediator.SendAsync(a);
                        await mediator.SendAsync(b);
                    }
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        var occurrences = CountOccurrences(result.GeneratedSource, "\"TestApp.Ping\"");
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void GenericWrapperDispatch_FallsBackToTheConstraint()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public abstract record StarCommand : ICommand;

                public sealed record MagnetarCommand : StarCommand;

                public sealed class MagnetarHandler : ICommandHandler<MagnetarCommand>
                {
                    public ValueTask HandleAsync(MagnetarCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire<T>(T command) where T : StarCommand => mediator.SendAsync(command);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The type parameter itself proves nothing; its class constraint is the evidence.
        Assert.Contains(
            DispatchSitePrefix + "\"TestApp.StarCommand\", " + KindPrefix + "Command, false)]",
            result.GeneratedSource);
    }

    [Fact]
    public void ManualRegistrations_AreRecordedInTheManifest()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                [ExcludeFromDiscovery]
                public sealed class ManualPingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands)
                        => commands.Register<ManualPingHandler>();
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("ManualRegistrationAttribute(\"TestApp.ManualPingHandler\")", result.GeneratedSource);
        Assert.DoesNotContain("HasOpaqueRegistrations", result.GeneratedSource);
    }

    [Fact]
    public void OpaqueRegistrations_RaiseTheManifestFlag()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

            namespace TestApp
            {
                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands, Type runtimeType)
                        => commands.Register(runtimeType);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // The registration is now a defect in its own right (ERGO018) — and the manifest
        // still records the opacity, because a consumer reading this assembly's manifest
        // has to know its coverage evidence is incomplete either way.
        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGO018");
        Assert.Contains("DispatchManifestAttribute(1, HasOpaqueRegistrations = true)", result.GeneratedSource);
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
