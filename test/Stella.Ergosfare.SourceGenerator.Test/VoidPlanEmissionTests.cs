namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// Compile-time void pipeline plan emission: a dispatchable command whose whole discovered
/// pipeline is a single default-discovery, default-group async handler gets an
/// <c>AddVoidPlan</c> root; any ambiguity — a second handler, an interceptor, a keyed or
/// grouped handler, a result contract — keeps the type on the plain dispatch roots only.
/// </summary>
public class VoidPlanEmissionTests
{
    [Fact]
    public void SoloAsyncHandler_EmitsTheVoidPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record SoloPing : ICommand;

                public sealed class SoloPingHandler : ICommandHandler<SoloPing>
                {
                    public ValueTask HandleAsync(SoloPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The handler has an accessible parameterless constructor and is not disposable,
        // so the plan carries the direct-construction factory.
        Assert.Contains(
            "GeneratedDispatchRoots.AddVoidPlan<global::TestApp.SoloPing, global::TestApp.SoloPingHandler>(static () => new global::TestApp.SoloPingHandler());",
            result.GeneratedSource);
    }

    [Fact]
    public void ConstructorDependency_EmitsTheProviderFactory()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public interface IGreeter { }

                public sealed record NeedyPing : ICommand;

                public sealed class NeedyPingHandler : ICommandHandler<NeedyPing>
                {
                    public NeedyPingHandler(IGreeter greeter, string dependency) { }

                    public ValueTask HandleAsync(NeedyPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // A single public constructor whose every parameter is a plain service resolution
        // is the one shape where the container's own selection has no choice — the plan
        // carries a provider-taking factory resolving each dependency from the
        // dispatching scope, exactly as container activation would.
        Assert.Contains(
            "GeneratedDispatchRoots.AddVoidPlan<global::TestApp.NeedyPing, global::TestApp.NeedyPingHandler>("
            + "static provider => new global::TestApp.NeedyPingHandler("
            + "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::TestApp.IGreeter>(provider), "
            + "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<string>(provider)));",
            result.GeneratedSource);
    }

    [Fact]
    public void KeyedConstructorDependency_EmitsTheKeyedProviderFactory()
    {
        var result = GeneratorTestHost.Run("""
            using Microsoft.Extensions.DependencyInjection;
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public interface IGreeter { }

                public sealed record KeyedNeedyPing : ICommand;

                public sealed class KeyedNeedyPingHandler : ICommandHandler<KeyedNeedyPing>
                {
                    public KeyedNeedyPingHandler([FromKeyedServices("primary")] IGreeter greeter) { }

                    public ValueTask HandleAsync(KeyedNeedyPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains(
            "GeneratedDispatchRoots.AddVoidPlan<global::TestApp.KeyedNeedyPing, global::TestApp.KeyedNeedyPingHandler>("
            + "static provider => new global::TestApp.KeyedNeedyPingHandler("
            + "global::Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions.GetRequiredKeyedService<global::TestApp.IGreeter>(provider, \"primary\")));",
            result.GeneratedSource);
    }

    [Fact]
    public void OptionalParameter_SuppressesTheFactoryButKeepsThePlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record DefaultyPing : ICommand;

                public sealed class DefaultyPingHandler : ICommandHandler<DefaultyPing>
                {
                    public DefaultyPingHandler(string dependency = "fallback") { }

                    public ValueTask HandleAsync(DefaultyPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // The container uses the default value only when no string service is registered —
        // content-dependent behavior the emission cannot reproduce. Plan only, no factory.
        Assert.Contains(
            "GeneratedDispatchRoots.AddVoidPlan<global::TestApp.DefaultyPing, global::TestApp.DefaultyPingHandler>();",
            result.GeneratedSource);
    }

    [Fact]
    public void AdditionalConstructor_SuppressesTheFactoryButKeepsThePlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record PickyPing : ICommand;

                public sealed class PickyPingHandler : ICommandHandler<PickyPing>
                {
                    public PickyPingHandler() { }

                    public PickyPingHandler(string dependency) { }

                    public ValueTask HandleAsync(PickyPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // The container's greedy constructor selection would pick the richer constructor;
        // a `new()` factory would silently drop that dependency — plan only, no factory.
        Assert.Contains(
            "GeneratedDispatchRoots.AddVoidPlan<global::TestApp.PickyPing, global::TestApp.PickyPingHandler>();",
            result.GeneratedSource);
    }

    [Fact]
    public void RequiredMember_SuppressesTheFactoryButKeepsThePlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record NamedPing : ICommand;

                public sealed class NamedPingHandler : ICommandHandler<NamedPing>
                {
                    public required string Name { get; init; }

                    public ValueTask HandleAsync(NamedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // An emitted `new()` would fail compilation with CS9035 — plan only, no factory.
        Assert.Contains(
            "GeneratedDispatchRoots.AddVoidPlan<global::TestApp.NamedPing, global::TestApp.NamedPingHandler>();",
            result.GeneratedSource);
    }

    [Fact]
    public void DisposableHandler_SuppressesTheFactoryButKeepsThePlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record LeakyPing : ICommand;

                public sealed class LeakyPingHandler : ICommandHandler<LeakyPing>, IDisposable
                {
                    public ValueTask HandleAsync(LeakyPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;

                    public void Dispose() { }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains(
            "GeneratedDispatchRoots.AddVoidPlan<global::TestApp.LeakyPing, global::TestApp.LeakyPingHandler>();",
            result.GeneratedSource);
    }

    [Fact]
    public void SecondHandler_SuppressesThePlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record DuoPing : ICommand;

                public sealed class FirstDuoPingHandler : ICommandHandler<DuoPing>
                {
                    public ValueTask HandleAsync(DuoPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }

                public sealed class SecondDuoPingHandler : ICommandHandler<DuoPing>
                {
                    public ValueTask HandleAsync(DuoPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddVoidPlan<", result.GeneratedSource);
        Assert.Contains("AddMessage<global::TestApp.DuoPing>", result.GeneratedSource);
    }

    [Fact]
    public void DiscoveredInterceptor_SuppressesThePlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record GuardedPing : ICommand;

                public sealed class GuardedPingHandler : ICommandHandler<GuardedPing>
                {
                    public ValueTask HandleAsync(GuardedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }

                public sealed class GuardedPingInterceptor : ICommandPreInterceptor<GuardedPing>
                {
                    public ValueTask<GuardedPing> HandleAsync(GuardedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddVoidPlan<", result.GeneratedSource);
    }

    [Fact]
    public void KeyedOrGroupedHandler_SuppressesThePlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record KeyedPing : ICommand;

                [DiscoveryKey("plans.keyed")]
                public sealed class KeyedPingHandler : ICommandHandler<KeyedPing>
                {
                    public ValueTask HandleAsync(KeyedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }

                public sealed record GroupedPing : ICommand;

                [Group("reporting")]
                public sealed class GroupedPingHandler : ICommandHandler<GroupedPing>
                {
                    public ValueTask HandleAsync(GroupedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddVoidPlan<", result.GeneratedSource);
    }

    [Fact]
    public void ResultContract_DoesNotProduceAVoidPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record TypedPing : ICommand<string>;

                public sealed class TypedPingHandler : ICommandHandler<TypedPing, string>
                {
                    public ValueTask<string> HandleAsync(TypedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(string.Empty);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddVoidPlan<", result.GeneratedSource);
        Assert.Contains("AddResult<global::TestApp.TypedPing, string>", result.GeneratedSource);

        // The same solo-async-handler shape on the result side produces the result plan
        // instead, factory included.
        Assert.Contains(
            "GeneratedDispatchRoots.AddResultPlan<global::TestApp.TypedPing, string, global::TestApp.TypedPingHandler>(static () => new global::TestApp.TypedPingHandler());",
            result.GeneratedSource);
    }

    [Fact]
    public void DiscoveredInterceptor_SuppressesTheResultPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record GuardedTypedPing : ICommand<string>;

                public sealed class GuardedTypedPingHandler : ICommandHandler<GuardedTypedPing, string>
                {
                    public ValueTask<string> HandleAsync(GuardedTypedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(string.Empty);
                }

                public sealed class GuardedTypedPingInterceptor : ICommandPreInterceptor<GuardedTypedPing>
                {
                    public ValueTask<GuardedTypedPing> HandleAsync(GuardedTypedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddResultPlan<", result.GeneratedSource);
    }

    [Fact]
    public void StreamContract_DoesNotProduceAResultPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Queries.Abstractions;
            using System.Collections.Generic;

            namespace TestApp
            {
                public sealed record NumberStream : IStreamQuery<int>;

                public sealed class NumberStreamHandler : IStreamQueryHandler<NumberStream, int>
                {
                    public IAsyncEnumerable<int> StreamAsync(NumberStream message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => throw new System.NotImplementedException();
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddResultPlan<", result.GeneratedSource);
        Assert.Contains("AddStream<global::TestApp.NumberStream, int>", result.GeneratedSource);
    }
}
