namespace Ergosfare.Core.Context;

public class ContextData: IContextData
{
    private readonly Dictionary<Type, object> _data = new();
    
    public T Get<T>()
    {
        return (T) _data[typeof(T)];
    }

    public void Set<T>(T value) where T : notnull
    {
        _data[typeof(T)] = value;
    }

    public bool Contains<T>()
    {
        return _data.ContainsKey(typeof(T));
    }

    public void Remove<T>()
    {
        _data.Remove(typeof(T));
    }
}