using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Registry;

namespace Stella.Ergosfare.Core.Test.Internal;

/// <summary>
/// Concurrency tests for <see cref="MessageRegistry"/>: registration is serialized by the
/// registry's gate while dispatch-time readers (enumeration, resolve-strategy lookups)
/// stay lock-free over immutable snapshots.
/// </summary>
public class MessageRegistryThreadSafetyTests
{
    private const int ThreadCount = 8;

    private static MessageRegistry CreateRegistry() => new(new HandlerDescriptorBuilderFactory());

    private static Type[] MessageTypes =>
    [
        typeof(Msg00), typeof(Msg01), typeof(Msg02), typeof(Msg03),
        typeof(Msg04), typeof(Msg05), typeof(Msg06), typeof(Msg07),
        typeof(Msg08), typeof(Msg09), typeof(Msg10), typeof(Msg11),
        typeof(Msg12), typeof(Msg13), typeof(Msg14), typeof(Msg15),
    ];

    private static Type[] PreInterceptorTypes =>
    [
        typeof(Pre0), typeof(Pre1), typeof(Pre2), typeof(Pre3),
        typeof(Pre4), typeof(Pre5), typeof(Pre6), typeof(Pre7),
    ];

    /// <summary>
    /// All threads register the same set of message types simultaneously; every type must
    /// be registered exactly once.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ParallelRegistration_OfDistinctMessageTypes_RegistersEachOnce()
    {
        var registry = CreateRegistry();
        var types = MessageTypes;
        using var barrier = new Barrier(ThreadCount);

        var tasks = Enumerable.Range(0, ThreadCount)
            .Select(_ => Task.Run(() =>
            {
                barrier.SignalAndWait();
                foreach (var type in types)
                {
                    registry.Register(type);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(types.Length, registry.Count);
        Assert.Equal(types.Length, registry.Select(d => d.MessageType).Distinct().Count());
    }

    /// <summary>
    /// All threads register the same handler type simultaneously; the check-then-act on
    /// the processed-types set must not produce duplicate descriptors.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ParallelRegistration_OfSameHandlerType_YieldsSingleDescriptor()
    {
        var registry = CreateRegistry();
        using var barrier = new Barrier(ThreadCount);

        var tasks = Enumerable.Range(0, ThreadCount)
            .Select(_ => Task.Run(() =>
            {
                barrier.SignalAndWait();
                registry.Register(typeof(ThreadMessageHandler));
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        var descriptor = Assert.Single(registry, d => d.MessageType == typeof(ThreadMessage));
        Assert.Single(descriptor.Handlers);
    }

    /// <summary>
    /// Interceptor types for an already-registered message are registered from many
    /// threads; the message descriptor must end up with exactly one entry per type
    /// (no lost updates, no duplicates, no torn stage arrays).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ParallelRegistration_OfInterceptorsForExistingMessage_LosesNothing()
    {
        var registry = CreateRegistry();
        registry.Register(typeof(ThreadMessage));

        await Task.WhenAll(PreInterceptorTypes
            .Select(type => Task.Run(() => registry.Register(type)))
            .ToArray());

        var descriptor = Assert.Single(registry, d => d.MessageType == typeof(ThreadMessage));
        Assert.Equal(PreInterceptorTypes.Length, descriptor.PreInterceptors.Count);
        Assert.Equal(PreInterceptorTypes.Length, descriptor.PreInterceptors.Select(d => d.HandlerType).Distinct().Count());
    }

    /// <summary>
    /// Enumerating the registry and running resolve-strategy lookups while another thread
    /// registers must never throw — readers observe immutable snapshots, so a concurrent
    /// registration can never invalidate an enumeration in progress.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Enumeration_WhileRegistering_DoesNotThrow()
    {
        var registry = CreateRegistry();
        var strategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);
        var types = MessageTypes;
        var stop = 0;

        var readers = Enumerable.Range(0, ThreadCount - 1)
            .Select(readerIndex => Task.Run(() =>
            {
                while (Volatile.Read(ref stop) == 0)
                {
                    var count = 0;
                    foreach (var descriptor in registry)
                    {
                        Assert.NotNull(descriptor.MessageType);
                        count++;
                    }

                    Assert.InRange(count, 0, types.Length);
                    _ = strategy.Find(typeof(Msg15));
                }
            }))
            .ToArray();

        try
        {
            for (var round = 0; round < 50; round++)
            {
                var freshRegistry = round == 0;
                foreach (var type in types)
                {
                    registry.Register(type);

                    if (freshRegistry)
                    {
                        // Give the readers real overlap with the growing phase.
                        Thread.Yield();
                    }
                }
            }
        }
        finally
        {
            Volatile.Write(ref stop, 1);
        }

        // Reader exceptions (torn enumerations would surface as
        // InvalidOperationException) propagate here and fail the test.
        await Task.WhenAll(readers);

        Assert.Equal(types.Length, registry.Count);
        Assert.NotNull(strategy.Find(typeof(Msg15)));
    }

    private sealed class ThreadMessage : IMessage;

    private sealed class ThreadMessageHandler : IAsyncHandler<ThreadMessage>
    {
        public ValueTask HandleAsync(ThreadMessage message, IExecutionContext context) => default;
    }

    private abstract class PreBase : IAsyncPreInterceptor<ThreadMessage>
    {
        public ValueTask<object> HandleAsync(ThreadMessage message, IExecutionContext context) => new(message);
    }

    private sealed class Pre0 : PreBase;
    private sealed class Pre1 : PreBase;
    private sealed class Pre2 : PreBase;
    private sealed class Pre3 : PreBase;
    private sealed class Pre4 : PreBase;
    private sealed class Pre5 : PreBase;
    private sealed class Pre6 : PreBase;
    private sealed class Pre7 : PreBase;

    private sealed class Msg00 : IMessage;
    private sealed class Msg01 : IMessage;
    private sealed class Msg02 : IMessage;
    private sealed class Msg03 : IMessage;
    private sealed class Msg04 : IMessage;
    private sealed class Msg05 : IMessage;
    private sealed class Msg06 : IMessage;
    private sealed class Msg07 : IMessage;
    private sealed class Msg08 : IMessage;
    private sealed class Msg09 : IMessage;
    private sealed class Msg10 : IMessage;
    private sealed class Msg11 : IMessage;
    private sealed class Msg12 : IMessage;
    private sealed class Msg13 : IMessage;
    private sealed class Msg14 : IMessage;
    private sealed class Msg15 : IMessage;
}
