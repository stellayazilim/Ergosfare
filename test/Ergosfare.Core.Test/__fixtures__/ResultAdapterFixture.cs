using Ergosfare.Context;
using Ergosfare.Contracts.Attributes;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Test.__fixtures__;

public class ResultAdapterFixtures
{

    public record ResultAdapterMessage : IMessage;
    
    public class ResultAdapterErrorOrHandler: IAsyncHandler<ResultAdapterMessage, ErrorOr<string>>
    {
        public Task<ErrorOr<string>> HandleAsync(ResultAdapterMessage message, IExecutionContext context)
        {
            var err = Error.Conflict();
            return Task.FromResult<ErrorOr<string>>(err);
        }
    }
    
    public class ResultAdapterExceptionInterceptor: IAsyncExceptionInterceptor<ResultAdapterMessage, ErrorOr<string>>
    {
        public static bool IsCalled;
        public Task<object> HandleAsync(ResultAdapterMessage message, ErrorOr<string> result, Exception exception, IExecutionContext context)
        {
            IsCalled = true;
            throw exception;
        }
    }

    public class ErrorOrAdapter: IResultAdapter
    {
        public bool CanAdapt(in object result)
        {
            return result is  IErrorOr;
            
        }

        public bool TryGetException(object result, out Exception? exception)
        {
            exception = null!;

            if (result is not IErrorOr errorOr)
                return false;

            if (!errorOr.IsError)
                return false;

            var errors = errorOr.Errors;
            if (errors is null || errors.Count == 0)
                return false;

            // map ErrorOr.Error -> Exception
            var exceptions = errors
                .Select(e => new AdaptedException($"{e.Code}: {e.Description}", result))
                .ToList();

            exception = exceptions.Count == 1
                ? exceptions[0]
                : new AggregateException("Multiple errors occurred", exceptions);
            
            return true;
        }
    }

    public static (IServiceProvider, IMessageMediator, IMessageRegistry) CreateFixture(
        List<IResultAdapter> adapters,
        List<Type>  handlers
        )
    {
        var services = new ServiceCollection()
            .AddErgosfare(options => options
                .ConfigureResultAdapters(b =>
                {
                    adapters.ForEach(a => b.Register(a.GetType()));
                }).AddCoreModule(b =>
                {
                    handlers.ForEach(h => b.Register(h));
                }))
            .BuildServiceProvider();
        
        var registry = services.GetRequiredService<IMessageRegistry>();
   
        
        return (services, services.GetRequiredService<IMessageMediator>(), registry);
    }



    public static MediateOptions<ResultAdapterMessage, Task<ErrorOr<string>>> GetStrategy(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IMessageRegistry>();
        var strategy = new SingleAsyncHandlerMediationStrategy<ResultAdapterMessage, ErrorOr<string>>(services.GetRequiredService<IResultAdapterService>());
    
        var mediateOptions = new MediateOptions<ResultAdapterMessage, Task<ErrorOr<string>>>
        {
            Groups = [GroupAttribute.DefaultGroupName],
            MessageResolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry),
            MessageMediationStrategy = strategy,
            CancellationToken = default
        };
        
        return mediateOptions;
    }
}