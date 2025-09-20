using Ergosfare.Core.Abstractions;
using System.Reflection;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;


/// <summary>
/// Unit tests for <see cref="ResultAdapterBuilder"/>.
/// Verifies registration of <see cref="IResultAdapter"/> implementations
/// through generic, type-based, and assembly-based registration.
/// </summary>
public class ResultAdapterBuilderTests
{
    /// <summary>
    /// A fake adapter implementation used for testing registrations.
    /// </summary>
    private class FakeAdapter : IResultAdapter
    {
        public bool CanAdapt(object result)
        {
            throw new NotImplementedException();
        }

        public bool TryGetException(object result, out Exception? exception)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Another fake adapter for verifying assembly-based registration.
    /// </summary>
    private class AnotherAdapter : IResultAdapter
    {
        public bool CanAdapt(object result)
        {
            throw new NotImplementedException();
        }

        public bool TryGetException(object result, out Exception? exception)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// A class that does not implement <see cref="IResultAdapter"/>.
    /// Used to verify that <see cref="ResultAdapterBuilder.Register(Type)"/>
    /// ignores non-a
    private class NotAnAdapter { }

    /// <summary>
    /// Fake implementation of <see cref="IResultAdapterService"/> that
    /// captures all registered adapters for assertions.
    /// </summary>
    private class FakeResultAdapterService : IResultAdapterService
    {
        public List<IResultAdapter> Registered { get; } = new();

        public void AddAdapter(IResultAdapter adapter)
        {
            Registered.Add(adapter);
        }

        public IEnumerable<IResultAdapter> GetAdapters()
        {
            throw new NotImplementedException();
        }

        public Exception? LookupException(object? result)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterBuilder.Register{TAdapter}"/>
    /// correctly registers an adapter when provided as a generic type.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Register_Generic_ShouldAddAdapter()
    {
        var service = new FakeResultAdapterService();
        var builder = new ResultAdapterBuilder(service);

        // act
        builder.Register<FakeAdapter>();

        // assert
        Assert.Single(service.Registered);
        Assert.IsType<FakeAdapter>(service.Registered.First());
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterBuilder.Register(Type)"/>
    /// correctly registers an adapter when a valid adapter type is provided.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Register_Type_ShouldAddAdapter()
    {
        var service = new FakeResultAdapterService();
        var builder = new ResultAdapterBuilder(service);

        // act
        builder.Register(typeof(FakeAdapter));

        // assert
        Assert.Single(service.Registered);
        Assert.IsType<FakeAdapter>(service.Registered.First());
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterBuilder.Register(Type)"/>
    /// does not register anything when the provided type does not implement <see cref="IResultAdapter"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Register_Type_ShouldIgnoreNonAdapterTypes()
    {
        var service = new FakeResultAdapterService();
        var builder = new ResultAdapterBuilder(service);

        // act
        builder.Register(typeof(NotAnAdapter)); // not an IResultAdapter

        // assert
        Assert.Empty(service.Registered);
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterBuilder.RegisterFromAssembly(Assembly)"/>
    /// registers all concrete <see cref="IResultAdapter"/> implementations from the given assembly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void RegisterFromAssembly_ShouldAddAllAdapters()
    {
        var service = new FakeResultAdapterService();
        var builder = new ResultAdapterBuilder(service);

        // act: scan current assembly (contains FakeAdapter + AnotherAdapter)
        builder.RegisterFromAssembly(Assembly.GetExecutingAssembly());

        // assert
        Assert.Contains(service.Registered, a => a is FakeAdapter);
        Assert.Contains(service.Registered, a => a is AnotherAdapter);
    }

    /// <summary>
    /// Verifies that multiple registration calls can be chained fluently
    /// using the same <see cref="ResultAdapterBuilder"/> instance.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Register_Methods_ShouldSupportChaining()
    {
        var service = new FakeResultAdapterService();
        var builder = new ResultAdapterBuilder(service);

        // act: call in a chain
        builder
            .Register<FakeAdapter>()
            .Register(typeof(AnotherAdapter))
            .RegisterFromAssembly(Assembly.GetExecutingAssembly());

        // assert: multiple adapters should be registered
        Assert.True(service.Registered.Count >= 2);
    }
}
