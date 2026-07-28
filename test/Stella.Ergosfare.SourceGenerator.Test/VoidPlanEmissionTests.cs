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
        Assert.Contains(
            "GeneratedDispatchRoots.AddVoidPlan<global::TestApp.SoloPing, global::TestApp.SoloPingHandler>();",
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
    }
}
