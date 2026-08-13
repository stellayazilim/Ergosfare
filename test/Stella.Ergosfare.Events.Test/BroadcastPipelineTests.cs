using System.Reflection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Contains unit tests for <see cref="EventMediator"/>'s interceptor-bearing broadcast lane,
/// validating handler execution and exception handling behavior.
/// </summary>
public class BroadcastPipelineTests
(ITestOutputHelper  testOutputHelper)
{
    /// <summary>
    /// A dispatch engine over a bare provider — the directly-constructed counterpart of
    /// what <c>AddErgosfare</c> registers.
    /// </summary>
    private static MessageDispatchEngine Engine(IServiceProvider services)
    {
        var factory = new MessageDependenciesFactory(services);

        return new MessageDispatchEngine(new PipelineExecutorCache(factory), factory);
    }

    /// <summary>
    /// Tests that <see cref="EventMediator.PublishAsync(IEvent, EventMediationSettings?, CancellationToken)"/> throws an exception
    /// when <c>ThrowIfNoHandlerFound</c> is set to true and no handler is found.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldThrowWhenNoHandlerFound()
    {
        // A container that composed nothing: the event has no pipeline at all, which is
        // exactly the "nothing will handle this" the flag under test decides about.
        var services = new ServiceCollection().BuildServiceProvider();
        var mediator = new EventMediator(Engine(services), services);
        Exception? exception = null;
        try
        {
           await mediator.PublishAsync(new StubNonGenericEvent(), groups: null, items: null, throwIfNoHandlerFound: true, CancellationToken.None);

        }
        catch (Exception ex)
        {
            exception = ex;
        }
        testOutputHelper.WriteLine(typeof(StubNonGenericEvent).FullName);
        Assert.NotNull(exception);
         
    }
    
    /// <summary>
    /// Tests that <see cref="EventMediator.PublishAsync(IEvent, EventMediationSettings?, CancellationToken)"/> does not throw an exception
    /// when <c>ThrowIfNoHandlerFound</c> is false and no handler is found.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldNotThrowWhenNoHandlerFound()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var mediator = new EventMediator(Engine(services), services);
        Exception? exception = null;
        try
        {
            await mediator.PublishAsync(new StubNonGenericEvent(), groups: null, items: null, throwIfNoHandlerFound: false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        Assert.Null(exception);
         
    }

    /// <summary>
    /// Tests that <see cref="EventMediator.PublishAsync(IEvent, EventMediationSettings?, CancellationToken)"/> correctly runs registered handlers.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldRunHandlers()
    {
        var services = new ServiceCollection()
            .AddErgosfare(builder =>
            {
                builder.AddEventModule(x => { x.Register<StubNonGenericEventHandler1>(); });
            })
            .BuildServiceProvider();
        var mediator = services.GetRequiredService<IPublisher>();
        await mediator.PublishAsync(new StubNonGenericEvent());
    }

    /// <summary>
    /// Tests that exceptions are correctly intercepted when a handler throws during <see cref="EventMediator.PublishAsync(IEvent, EventMediationSettings?, CancellationToken)"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldThrowWhileRunningHandlers()
    {
        var services = new ServiceCollection()
            .AddErgosfare(builder =>
            {
                builder.AddEventModule(x =>
                {
                    x.Register<StubNonGenericEventHandlerThrows>();
                    x.Register<StubNonGenericEventExceptionInterceptor>();
                });
            })
            .BuildServiceProvider();
        var mediator = services.GetRequiredService<IPublisher>();
        await mediator.PublishAsync(new StubNonGenericEventThrows());
        Assert.True(StubNonGenericEventHandlerThrows.IsRuned);
        Assert.True(StubNonGenericEventExceptionInterceptor.IsRuned);
    }
}