using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Queries.Abstractions;

namespace Ergosfare.Queries;

public class QueryMediator(IMessageMediator messageMediator): IQueryMediator
{
    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, QueryMediationSettings? queryMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        queryMediationSettings ??= new QueryMediationSettings();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<IQuery<TResult>, TResult>();
        var resolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        var resultStrategy = new ErgoResultStrategy<TResult>();
        return messageMediator.Mediate(query,
            new MediateOptions<IQuery<TResult>, Task<TResult>>
            {
                ResultStrategy = resultStrategy,
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = resolveStrategy,
                CancellationToken = cancellationToken,
                Items = queryMediationSettings.Items
            });
    }

    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, QueryMediationSettings? queryMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        queryMediationSettings ??= new QueryMediationSettings();
        var mediationStrategy = new SingleStreamHandlerMediationStrategy<IStreamQuery<TResult>, TResult>(cancellationToken);
        var resolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        var resultStrategy = new ErgoResultStrategy<ErgoResult<TResult>>();
        return messageMediator.Mediate(query,
            new MediateOptions<IStreamQuery<TResult>, IAsyncEnumerable<TResult>>
            {
                ResultStrategy = resultStrategy,
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = resolveStrategy,
                CancellationToken = cancellationToken,
                Items = queryMediationSettings.Items
            });

    }
}