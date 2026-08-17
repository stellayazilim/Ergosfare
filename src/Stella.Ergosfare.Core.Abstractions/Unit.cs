namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The value carried in the result slot of a pipeline that produces no result.
/// </summary>
/// <remarks>
/// The post-, exception- and final-interceptor stages take a result argument on every path,
/// including void dispatches. On those paths they receive <see cref="Value"/>.
/// </remarks>
public sealed class Unit
{
    /// <summary>
    /// Initializes the single instance; no other instance can be constructed.
    /// </summary>
    private Unit()
    {
    }

    /// <summary>
    /// The single instance, shared process-wide. A resultless pipeline supplies no other
    /// value, so interceptors may compare against it by reference.
    /// </summary>
    public static readonly Unit Value = new();
}
