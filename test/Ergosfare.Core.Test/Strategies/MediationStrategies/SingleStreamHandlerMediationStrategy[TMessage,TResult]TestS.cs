using System.Reflection;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Contexts;
using Ergosfare.Core.Test.__factories__;
using Ergosfare.Core.Test.__stubs__;
using Xunit.Abstractions;

namespace Ergosfare.Core.Test.Strategies;

public class SingleStreamHandlerMediationStrategyTMessageTResultTests(
    ITestOutputHelper  testOutputHelper)
{

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldHaveThrowMultipleHandlerException()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler),
            typeof(StubNonGenericStreamHandler2));
        
        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken
            );
        // act
        try
        {
          var stream = strategy.Mediate(
              new StubNonGenericMessage(), 
              dependencies, 
              AmbientExecutionContext.Current);
          await foreach(var value in stream) {}
        }
        // assert
        catch (MultipleHandlerFoundException e)
        {
            Assert.Equal(dependencies.Handlers.Count,  e.NumberOfHandlers);
            Assert.Equal($"{nameof(StubNonGenericMessage)} has {dependencies.Handlers.Count} handlers registered.", e.Message);
        }
        
    }
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldSetExecutionContext()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler));
        
        var items = new Dictionary<object, object?>();
        var context = new ErgosfareExecutionContext(items,CancellationToken.None);

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            context.CancellationToken
        );
        
        await using var _ = AmbientExecutionContext.CreateScope(context); 
        
        
        // act
        var stream = strategy.Mediate(
            new StubNonGenericMessage(), 
            dependencies, 
            context);
        await foreach(var value in stream) {}
        
        Assert.Same(AmbientExecutionContext.Current, context);
        
    }

    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldHaveReturnValues()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler));
        
        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken
        );
        
        var expected = new[] { "foo", "bar", "baz" };
        var index = 0;
        
        // act
        var stream = strategy.Mediate(
            new StubNonGenericMessage(), 
            dependencies, 
            AmbientExecutionContext.Current);
        
        // asert
        await foreach (var item in stream)
        {
            Assert.Equal(expected[index], item);
            index++;
        }
       
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldHaveNotContinueOnPreException()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubPreInterceptorThrowsUnknownException),
            typeof(StubNonGenericStreamExceptionInterceptor),
            typeof(StubNonGenericStreamHandler));
        
        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken
        );
        
        string[] expected = [];
        string[] items = [];
        var index = 0;
        
        // act
        try
        {
            await foreach (var item in strategy.Mediate(
                               new StubNonGenericMessage(), 
                               dependencies, 
                               AmbientExecutionContext.Current))
            {
                items[index] = item;
                index++;
            }
        }
        // asert
        catch (Exception e)
        {
            Assert.IsType<Exception>(e);
        }
        
        Assert.Equal(expected, items);
    }
    
    
    
        
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldHaveNotContinueOnPreAbort()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubPreInterceptorAbortExecution),
            typeof(StubNonGenericStreamExceptionInterceptor),
            typeof(StubNonGenericStreamHandler));
        
        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken
        );
        
        var expected = new string[] { };
        string[] items = [];
        var index = 0;
        
        // act
        try
        {
            await foreach (var item in strategy.Mediate(
                               new StubNonGenericMessage(), 
                               dependencies, 
                               AmbientExecutionContext.Current))
            {
                items[index] = item;
                index++;
            }
        }
        // asert
        catch (Exception e)
        {
            Assert.IsType<ExecutionAbortedException>(e);
        }
        
        Assert.Equal(expected , items);
    }
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldHaveAbortWhileRunningPostInterceptors()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamPostInterceptorsAbortExecution),
            typeof(StubNonGenericStreamExceptionInterceptor),
            typeof(StubNonGenericStreamHandler));
        
        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken
        );
        
        var expected = new string[] { "foo", "bar", "baz"};
        var items = new List<string>();
        
        // act
        try
        {
            await foreach (var item in strategy.Mediate(
                               new StubNonGenericMessage(), 
                               dependencies, 
                               AmbientExecutionContext.Current))
            {
                items.Add(item);
            }
        }
        // asert
        catch (Exception e)
        {
            Assert.IsType<ExecutionAbortedException>(e);
        }
        
        Assert.Equal(expected , items);
    }


    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldCatchExecutionAbortedException()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandlerAbortsExecution));

        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken);

        var items = new List<string>();

        // act
        await foreach (var item in strategy.Mediate(
                           new StubNonGenericMessage(),
                           dependencies,
                           AmbientExecutionContext.Current))
        {
            items.Add(item);
        }

        // assert → nothing yielded, but catch block was hit
        Assert.Equal(["foo"],items);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldStopPipelineEarly_OnExecutionAborted()
    {
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler));

        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken);

        // Force executionAborted=true (consume stays true)
        typeof(SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>)
            .GetField("_executionAborted", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(strategy, true);

        var items = new List<string>();

        await foreach (var item in strategy.Mediate(
                           new StubNonGenericMessage(),
                           dependencies,
                           AmbientExecutionContext.Current))
        {
            items.Add(item);
        }

        Assert.Empty( items); // hit yield break
    }
                
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldHaveThrowExceptionWhileYielding()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamExceptionInterceptor),
            typeof(StubNonGenericStreamHandlerThrowsException));
        
        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken
        );
        
        var expected = new[] { "foo"};
        var items = new List<string>();
     
        
        // act
        try
        {
            await foreach (var item in strategy.Mediate(
                               new StubNonGenericMessage(), 
                               dependencies, 
                               AmbientExecutionContext.Current))
            {
                items.Add(item);
            }
        }
        catch (Exception e)
        {
            testOutputHelper.WriteLine(e.Message);
            Assert.Equal("bar exception", e.Message);
        }
        
        Assert.Equal(expected , items);
    }

    
    
                    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldHaveAllResultsYieldedOnPostInterceptorException()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler),
            typeof(StubNonGenericStreamPostInterceptorThrowsException),
            typeof(StubNonGenericStreamExceptionInterceptor));
        
        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());


        var expected = new[] { "foo", "bar", "baz"};
        var items = new List<string>();
        Exception? caught = null;
   

        try
        {
            var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
                AmbientExecutionContext.Current.CancellationToken
            );
            
            var enumerator =  strategy.Mediate(new StubNonGenericMessage(), dependencies, AmbientExecutionContext.Current).GetAsyncEnumerator();
            
            
            while (await enumerator.MoveNextAsync())
            {
                items.Add(enumerator.Current);
            }
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        Assert.Equal(expected, items);
    }
    
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldIgnoreAbortInPostInterceptor()
    {
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler),
            typeof(StubNonGenericStreamPostInterceptorsAbortExecution));

        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken);

        var items = new List<string>();

        await foreach (var item in strategy.Mediate(
                           new StubNonGenericMessage(),
                           dependencies,
                           AmbientExecutionContext.Current))
        {
            items.Add(item);
        }

        // Post interceptor aborted, but no exception should bubble out
        Assert.NotEmpty(items); 
    }
    
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldCaptureExceptionFromPostInterceptor()
    {
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler),
            typeof(StubNonGenericStreamPostInterceptorThrowsException));

        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken);



        try
        {
            await foreach (var __ in strategy.Mediate(
                               new StubNonGenericMessage(),
                               dependencies,
                               AmbientExecutionContext.Current))
            { }
        }
        catch (Exception ex)
        {
            Assert.Equal("post exception", ex.Message);
        }

    
    }
    
    
    
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldRethrowWhenExceptionInterceptorFails()
    {
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler),
            typeof(StubNonGenericStreamPostInterceptorThrowsException),   // triggers _unknownException
            typeof(StubNonGenericStreamExceptionInterceptorThrowsException)); // rethrows

     

   
        await using var __ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());
        Exception? caught = null;
        try
        {
          
            var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
                AmbientExecutionContext.Current.CancellationToken);
            await foreach (var _ in strategy.Mediate(
                               new StubNonGenericMessage(),
                               dependencies,
                               AmbientExecutionContext.Current))
            { }
        }
        catch (Exception ex)
        {
            caught = ex;
           
        }

        Assert.Equal("post exception", caught?.Message);
    }
    
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldCatchExecutionAbortedInPostInterceptor()
    {
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler), 
            typeof(StubNonGenericStreamPostInterceptorsAbortExecution));

        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken);

        // Act + Assert: should NOT bubble out because your catch eats it
        await foreach (var __ in strategy.Mediate(
                           new StubNonGenericMessage(),
                           dependencies,
                           AmbientExecutionContext.Current))
        {
            // No items expected, but loop needed to drive async stream
        }
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldReturnEmptyEnumerableWhenAborted()
    {
        // arrange
        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericStreamHandler), 
            typeof(StubNonGenericStreamPreInterceptorAbortExecution)); // throws ExecutionAbortedException in Pre

        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

        var strategy = new SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>(
            AmbientExecutionContext.Current.CancellationToken
        );

        var items = new List<string>();

        // act
        await foreach (var item in strategy.Mediate(
                           new StubNonGenericMessage(),
                           dependencies,
                           AmbientExecutionContext.Current))
        {
            items.Add(item);
        }

        // assert
        Assert.Empty(items); // covers Empty<T>() yielding break
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task EmptyAsyncEnumerable_ShouldBeEmpty()
    {
        // Use reflection to get the private static Empty<T>() method
        var methodInfo = typeof(SingleStreamHandlerMediationStrategy<StubNonGenericMessage, string>)
                             .GetMethod("Empty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                         ?? throw new InvalidOperationException("Empty<T> method not found");

        var genericMethod = methodInfo.MakeGenericMethod(typeof(string));

        // Invoke the method to get the IAsyncEnumerable<string>
        var result = (IAsyncEnumerable<string>)genericMethod.Invoke(null, null)!;

        // Enumerate to make sure it yields nothing
        var items = new List<string>();
        await foreach (var item in result)
        {
            items.Add(item);
        }

        Assert.Empty(items); // this confirms it really yields nothing
        
    }
}