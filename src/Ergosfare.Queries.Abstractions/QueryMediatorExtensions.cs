using Ergosfare.Contracts;

namespace Ergosfare.Queries.Abstractions;

public static class QueryMediatorExtensions
{
    public static Task<TResult> QueryAsync<TResult>(
        this IQueryMediator queryMediator,
        IQuery<TResult> query,
        CancellationToken cancellationToken = default
    )
    {
        return queryMediator.QueryAsync(query, null, cancellationToken);
    }



    public static Task<TResult> QueryAsync<TResult>(
        this IQueryMediator queryMediator,
        IQuery<TResult> query,
        string[] groups,
        CancellationToken cancellationToken = default)
    {
        return queryMediator.QueryAsync(
            query, new QueryMediationSettings()
            {
                Filters = { Groups = groups }
            }, cancellationToken );
    }
    
    
    
    public static IAsyncEnumerable<TResult> StreamAsync<TResult>(
        this IQueryMediator queryMediator,
        IStreamQuery<TResult> query,
        CancellationToken cancellationToken = default
    )
    {
        return queryMediator.StreamAsync(query, null, cancellationToken);
    }


    public static IAsyncEnumerable<TResult> StreamAsync<TResult>(
        this IQueryMediator queryMediator,
        IStreamQuery<TResult> query,
        string[] groups,
        CancellationToken cancellationToken = default
    )
    {
        return queryMediator.StreamAsync(query, new QueryMediationSettings()
        {
            Filters = { Groups = groups }
        }, cancellationToken );
    } 
}