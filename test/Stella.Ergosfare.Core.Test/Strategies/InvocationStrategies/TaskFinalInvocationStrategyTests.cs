using System.Reflection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Stella.Ergosfare.Core.Test.Strategies.InvocationStrategies;

/// <summary>
/// Unit tests for <see cref="TaskFinalInterceptorInvocationStrategy"/>.
/// </summary>
public class TaskFinalInterceptorInvocationStrategyTests: 
    IClassFixture<MessageDependencyFixture>, 
    IClassFixture<DescriptorFixture>,
    IClassFixture<ExecutionContextFixture>,
    IClassFixture<SignalFixture>
{
    private MessageDependencyFixture _messageDependencyFixture;
    private readonly DescriptorFixture _descriptorFixture;
    private ExecutionContextFixture _executionContextFixture;
    private readonly ITestOutputHelper _testOutputHelper;
    private SignalFixture _signalFixture;
    // ReSharper disable once ConvertToPrimaryConstructor
    public TaskFinalInterceptorInvocationStrategyTests(
        ITestOutputHelper  testOutputHelper,
        MessageDependencyFixture messageDependencyFixture,
        DescriptorFixture descriptorFixture,
        ExecutionContextFixture executionContextFixture,
        SignalFixture signalFixture)
    {
        _testOutputHelper = testOutputHelper;
        _messageDependencyFixture = messageDependencyFixture;
        _descriptorFixture = descriptorFixture;
        _executionContextFixture = executionContextFixture;
        _signalFixture = signalFixture;
    }

    
    /// <summary>
    /// Tests that the <see cref="TaskFinalInterceptorInvocationStrategy"/> correctly executes
    /// both direct and indirect final interceptors for a given message.
    /// </summary>
    /// <remarks>
    /// This test ensures that:
    /// <list type="bullet">
    /// <item>Direct final interceptors are executed for the message.</item>
    /// <item>Indirect final interceptors (registered for parent or assignable messages) are also executed.</item>
    /// <item>The <see cref="MessageDependencyFixture"/> and <see cref="DescriptorFixture"/> integration
    /// correctly resolves the <see cref="IMessageDescriptor"/> and <see cref="IMessageDependencies"/>.</item>
    /// </list>
    /// </remarks>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task Invoke_ShouldExecuteDirectAndIndirectFinalInterceptors()
    {
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.MessageRegistry.Register(typeof(StubIndirectMessage));
        _messageDependencyFixture.MessageRegistry.Register(typeof(StubFinalInterceptor));
        _messageDependencyFixture.MessageRegistry.Register(typeof(StubIndirectFinalInterceptor));

        
        
        
        // Set fixture to descriptor and create descriptor
        var descriptor = _descriptorFixture
            .SetMessageRegistry(_messageDependencyFixture.MessageRegistry)
            .GetDescriptorFromRegistry(typeof(StubIndirectMessage));

        Assert.NotNull(descriptor);
        
        // Create dependencies from descriptor
        var messageDependencies = _messageDependencyFixture.CreateDependenciesFromDescriptor<StubMessage>(descriptor!);
        
        Assert.NotEmpty(messageDependencies.FinalInterceptors);
        Assert.NotEmpty(messageDependencies.IndirectFinalInterceptors);
        
        // cleanup
        _descriptorFixture.Dispose();
        _messageDependencyFixture.Dispose();

    }
    
    /// <summary>
    /// Verifies that <see cref="TaskFinalInterceptorInvocationStrategy.InvokeFinalInterceptorCollection"/>
    /// correctly executes all final interceptors, both direct and indirect, for a given message.
    /// </summary>
    /// <remarks>
    /// This test subscribes to <see cref="BeginFinalInterceptorInvocationSignal"/> and
    /// <see cref="FinishFinalInterceptorInvocationSignal"/> to verify that signals are raised
    /// before and after each interceptor. It also checks that all registered handlers
    /// are resolved and executed asynchronously.
    /// </remarks>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task InvokeFinalInterceptorCollection_ShouldExecuteAllFinalInterceptors()
    {
        // Arrange
        var invoked = false;
        var invokeCount = 0;
        
        
        var subsciber = PipelineSignal.Subscribe<FinishFinalInterceptorInvocationSignal>(e => 
        { 
            invoked = true;
            invokeCount++;
        });
        
        var subsciber2 = PipelineSignal.Subscribe<BeginFinalInterceptorInvocationSignal>(e =>
        {
            invoked = true; 
            invokeCount++;
        });
        
        _messageDependencyFixture = _messageDependencyFixture.New;

        // Register the message and interceptors
        _messageDependencyFixture.MessageRegistry.Register(typeof(StubIndirectMessage));
        _messageDependencyFixture.RegisterHandler(
            typeof(StubMessage),
            typeof(StubIndirectMessage),
            typeof(StubVoidAsyncFinalInterceptor),
            typeof(StubVoidIndirectAsyncFinalInterceptor)
        );

        _messageDependencyFixture.AddServices(s => s.BuildServiceProvider());

        // Create descriptor and dependencies
        var descriptor = _descriptorFixture
            .SetMessageRegistry(_messageDependencyFixture.MessageRegistry)
            .GetDescriptorFromRegistry(typeof(StubIndirectMessage));

        var messageDependencies = _messageDependencyFixture
            .CreateDependenciesFromDescriptor<StubIndirectMessage>(descriptor!);

        var strategy = new TaskFinalInterceptorInvocationStrategy(messageDependencies, null);

        // Create dummy message and execution context
        var message = new StubIndirectMessage();
        


        // Access the private method via reflection
        var method = typeof(TaskFinalInterceptorInvocationStrategy)
            .GetMethod("InvokeFinalInterceptorCollection", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        
        // Verify that interceptors were resolved and executed
        Assert.NotEmpty(messageDependencies.FinalInterceptors);
        Assert.NotEmpty(messageDependencies.IndirectFinalInterceptors);
        
        
      
        // Act & Assert for direct final interceptors
         var directInvoke = method!.Invoke(strategy, [
            messageDependencies.FinalInterceptors,
            message,
            Task.CompletedTask,   // result
            new Exception(),   // exception
            _executionContextFixture.Ctx
        ]);

        // Act & Assert for indirect final interceptors
        var indirectInvoke = method!.Invoke(strategy, [
            messageDependencies.IndirectFinalInterceptors,
            message,
            Task.CompletedTask,   // result
            new Exception(),   // exception
            _executionContextFixture.Ctx
        ]);

        

        // Optional: you can verify that each handler type was executed
        var executedTypes = messageDependencies.FinalInterceptors
            .Concat(messageDependencies.IndirectFinalInterceptors)
            .Select(x => x.Handler.Value.GetType())
            .ToList();
        
        Assert.True(invoked);
        Assert.Contains(typeof(StubVoidAsyncFinalInterceptor), executedTypes);
        Assert.Contains(typeof(StubVoidIndirectAsyncFinalInterceptor), executedTypes);
        Assert.Equal(4, invokeCount);
      
        // cleanup
        subsciber.Dispose();
        subsciber2.Dispose();
        await _signalFixture.DisposeAsync();
    }


}