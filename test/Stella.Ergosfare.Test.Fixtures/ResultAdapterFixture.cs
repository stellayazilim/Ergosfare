using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Test.Fixtures;

/// <summary>
/// Provides fixtures and stubs for testing <see cref="ResultAdapterService"/> and <see cref="IResultAdapter"/> behavior.
/// Implements <see cref="IFixture{ResultAdapterFixtures}"/> for a consistent fixture API.
/// </summary>
public class ResultAdapterFixtures : IFixture<ResultAdapterFixtures>
{
    private bool _disposed;
    private readonly Lazy<ServiceProvider> _lazyProvider;
    private readonly IServiceCollection _services;

    /// <summary>
    /// The main <see cref="ResultAdapterService"/> used by this fixture.
    /// </summary>
    public readonly ResultAdapterService ResultAdapterService = new ResultAdapterService();

    /// <summary>
    /// Gets the service provider built from the registered services.
    /// </summary>
    public ServiceProvider ServiceProvider => _lazyProvider.Value;

    /// <summary>
    /// Creates a fresh, independent instance of <see cref="ResultAdapterFixtures"/>.
    /// </summary>
    public ResultAdapterFixtures New => new();

    /// <summary>
    /// A stub message used for testing result adapters.
    /// </summary>
    public record ResultAdapterMessage : IMessage;

    /// <summary>
    /// Creates an <see cref="ErrorOr{T}"/> representing a failed result.
    /// </summary>
    public static ErrorOr<TResult> CreateError<TResult>() where TResult : notnull => Error.Conflict();

    /// <summary>
    /// Creates an <see cref="ErrorOr{T}"/> representing a successful result.
    /// </summary>
    public static ErrorOr<TResult> CreateResult<TResult>(TResult result) where TResult : notnull => result;

    #region Stub Handlers and Interceptors

    /// <summary>
    /// A stub handler returning an <see cref="ErrorOr{T}"/> in a failed state (exception scenario).
    /// </summary>
    public class ResultAdapterErrorOrHandlerReturnException : IAsyncHandler<ResultAdapterMessage, ErrorOr<string>>
    {
        public async Task<ErrorOr<string>> HandleAsync(ResultAdapterMessage message, IExecutionContext context)
        {
            await Task.CompletedTask;
            return CreateError<string>();
        }
    }

    /// <summary>
    /// A stub handler returning an <see cref="ErrorOr{T}"/> as a successful result.
    /// </summary>
    public class ResultAdapterErrorOrHandlerReturnResult : IAsyncHandler<ResultAdapterMessage, ErrorOr<string>>
    {
        /// <summary>
        /// The predefined string result returned by this handler.
        /// </summary>
        public const string Result = "Hello world";

        public async Task<ErrorOr<string>> HandleAsync(ResultAdapterMessage message, IExecutionContext context)
        {
            await Task.CompletedTask;
            return CreateResult<string>(Result);
        }
    }

    /// <summary>
    /// A post-interceptor that converts the handler result into a failed <see cref="ErrorOr{T}"/>.
    /// </summary>
    public class ResultAdapterErrorOrPostInterceptorReturnException : IAsyncPostInterceptor<ResultAdapterMessage, ErrorOr<string>>
    {
        public async Task<object> HandleAsync(ResultAdapterMessage message, ErrorOr<string> messageResult, IExecutionContext context)
        {
            await Task.CompletedTask;
            return CreateError<string>();
        }
    }

    /// <summary>
    /// An exception interceptor that rethrows exceptions instead of returning a result.
    /// Used to test exception propagation from result adapters.
    /// </summary>
    public class ResultAdapterExceptionInterceptor : IAsyncExceptionInterceptor<ResultAdapterMessage, ErrorOr<string>>
    {
        /// <summary>
        /// Flag indicating whether the interceptor was called.
        /// </summary>
        public static bool IsCalled;

        public Task<object> HandleAsync(ResultAdapterMessage message, ErrorOr<string> result, Exception exception, IExecutionContext context)
        {
            IsCalled = true;
            throw exception;
        }
    }

    #endregion

    #region Adapters

    /// <summary>
    /// Adapter that can adapt <see cref="IErrorOr"/> or <see cref="Error"/> results into exceptions.
    /// </summary>
    public class ErrorOrAdapter : IResultAdapter
    {
        public bool CanAdapt(object result)
        {
            return result is IErrorOr or Error;
        }

        public bool TryGetException(object result, out Exception? exception)
        {
            exception = null;
            if (result is not Error errorOr)
                return false;

            // Explicitly check if it's an error state
            exception = new AdaptedException(errorOr.Description ?? "Unknown error", result);
            return true;
        }
    }

    
    /// <summary>
    /// A fake adapter that always claims it can adapt and returns a fixed exception.
    /// Used to verify successful exception lookup.
    /// </summary>
    public class AlwaysAdaptAdapter : IResultAdapter
    {
        public bool CanAdapt(object result) => true;

        public bool TryGetException(object result, out Exception? exception)
        {
            exception = new InvalidOperationException("adapted");
            return true;
        }
    }
    
    
    /// <summary>
    /// A fake adapter that claims it can adapt but fails to produce an exception.
    /// Used to test the case where an adapter accepts input but yields no exception.
    /// </summary>
    public class NullExceptionAdapter : IResultAdapter
    {
        public bool CanAdapt(object result) => true;

        public bool TryGetException(object result, out Exception? exception)
        {
            exception = null;
            return false;
        }
    }
    
    
    /// <summary>
    /// A fake adapter that never claims it can adapt any input.
    /// Used to verify that unrelated adapters are skipped.
    /// </summary>
    public class NeverAdaptAdapter : IResultAdapter
    {
        public bool CanAdapt(object result) => false;

        public bool TryGetException(object result, out Exception? exception)
        {
            exception = null;
            return false;
        }
    }
    #endregion

    #region Fixture Initialization

    /// <summary>
    /// Registers services and adapters for this fixture.
    /// </summary>
    public ResultAdapterFixtures AddServices(Action<IServiceCollection> configure)
    {
        _services.AddSingleton(ResultAdapterService);
        configure(_services);
        return this;
    }

    /// <summary>
    /// Initializes the fixture and registers the <see cref="ErrorOrAdapter"/>.
    /// </summary>
    public ResultAdapterFixtures()
    {
        ResultAdapterService.AddAdapter(new ErrorOrAdapter());
        _services = new ServiceCollection();
        _lazyProvider = new Lazy<ServiceProvider>(() => _services.BuildServiceProvider());
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the fixture, including the underlying <see cref="ServiceProvider"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_lazyProvider.IsValueCreated)
            ServiceProvider.Dispose();
        _disposed = true;
    }

    #endregion
}
