namespace Ergosfare.Core.Context;

public interface IContextData
{
    T Get<T>();

    void Set<T>(T value) where T : notnull;
    
    bool Contains<T>();
    
    void Remove<T>();
}