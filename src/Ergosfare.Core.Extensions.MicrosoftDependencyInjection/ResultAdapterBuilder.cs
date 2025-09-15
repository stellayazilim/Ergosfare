using System.Reflection;
using Ergosfare.Core.Abstractions;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public class ResultAdapterBuilder(IResultAdapterService resultAdapterService)
{
    public ResultAdapterBuilder Register<TAdapter>() where TAdapter : IResultAdapter, new()
    {
        resultAdapterService.AddAdapter(new TAdapter());
        return this;
    }
    
    
    public ResultAdapterBuilder Register(Type adapter) 
    {
        if (Activator.CreateInstance(adapter) is  IResultAdapter resultAdapter)
            resultAdapterService.AddAdapter(resultAdapter);
        return this;
    }


    
    public ResultAdapterBuilder RegisterFromAssembly(Assembly assembly)
    {
        foreach (var adapter in assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(IResultAdapter)) && t is { IsClass: true, IsAbstract: false }))
        {
            if (Activator.CreateInstance(adapter) is  IResultAdapter resultAdapter)
                resultAdapterService.AddAdapter(resultAdapter);
        }

        return this;
    }
}