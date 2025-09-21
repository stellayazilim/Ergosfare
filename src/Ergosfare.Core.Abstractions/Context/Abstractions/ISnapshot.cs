namespace Ergosfare.Core.Abstractions;

/// <summary>
/// Represents a snapshot of a computation or intermediate step.
/// Provides access to the captured result in a non-generic form.
/// A snapshot result is local to its step and not the final pipeline result.
/// </summary>
public interface ISnapshot
{
    /// <summary>
    /// Gets the captured result of this snapshot as an <see cref="object"/>.
    /// Use the generic <see cref="ISnapshot{TResult}"/> to access the strongly-typed result.
    /// </summary>
    public object Result { get; internal set;  }
}