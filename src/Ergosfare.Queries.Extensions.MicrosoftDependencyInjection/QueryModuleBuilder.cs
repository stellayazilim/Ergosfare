using System.Reflection;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Queries.Abstractions;

namespace Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
public sealed class QueryModuleBuilder(IMessageRegistry messageRegistry)
{
    private readonly IMessageRegistry _messageRegistry = messageRegistry ?? throw new ArgumentNullException(nameof(messageRegistry));

    public QueryModuleBuilder Register<TQuery>() where TQuery : IQuery
    {
        _messageRegistry.Register(typeof(TQuery));
        return this;
    }

    public QueryModuleBuilder Register(Type queryType)
    {
        if (!queryType.IsAssignableTo(typeof(IQuery)))    
            throw new NotSupportedException($"The given type '{queryType.Name}' is not a query construct and cannot be registered.");
        
        _messageRegistry.Register(queryType);
        return this;

    }

    public QueryModuleBuilder RegisterFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(IQuery))))
        {
            _messageRegistry.Register(type);
        }
        
        return this;
    }
}