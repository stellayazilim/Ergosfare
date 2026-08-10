namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The single value a pipeline without a result carries in its result slot. A void
/// dispatch produces nothing, but the post-, exception- and final-interceptor stages all
/// take a result argument — <see cref="Value"/> is what they are handed, on every path.
/// </summary>
/// <remarks>
/// <para>
/// A reference type on purpose. The result slot travels as <c>object?</c> and is cast back
/// to the pipeline's result type at each stage, so a value-typed stand-in cost a boxing on
/// the way in and turned an empty slot into a <see cref="NullReferenceException"/> on the
/// way out. One shared instance boxes nothing and casts from <c>null</c> harmlessly.
/// </para>
/// <para>
/// This is the result <em>representation</em>, not the completion signal: a void handler
/// still returns <see cref="ValueTask"/>, and so do the mediation strategies. Only what
/// sits in the slot changed.
/// </para>
/// </remarks>
public sealed class Unit
{
    /// <summary>Prevents any instance but <see cref="Value"/> from existing.</summary>
    private Unit()
    {
    }

    /// <summary>
    /// The one instance, shared process-wide. Interceptors may compare against it by
    /// reference: a resultless pipeline never hands out anything else.
    /// </summary>
    public static readonly Unit Value = new();
}
