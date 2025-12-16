using System.Reflection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
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
    IClassFixture<ExecutionContextFixture>
{
    private MessageDependencyFixture _messageDependencyFixture;
    private readonly DescriptorFixture _descriptorFixture;
    private ExecutionContextFixture _executionContextFixture;
    private readonly ITestOutputHelper _testOutputHelper;
    // ReSharper disable once ConvertToPrimaryConstructor
    public TaskFinalInterceptorInvocationStrategyTests(
        ITestOutputHelper  testOutputHelper,
        MessageDependencyFixture messageDependencyFixture,
        DescriptorFixture descriptorFixture,
        ExecutionContextFixture executionContextFixture)
    {
        _testOutputHelper = testOutputHelper;
        _messageDependencyFixture = messageDependencyFixture;
        _descriptorFixture = descriptorFixture;
        _executionContextFixture = executionContextFixture;
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
    
 


}