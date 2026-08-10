using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;

namespace Stella.Ergosfare.Core.Internal.Contexts;


/// <summary>
/// <inheritdoc cref="IExecutionContext"/>
/// </summary>
/// <remarks>
/// Instances are pooled: a dispatch rents one from
/// <see cref="ErgosfareExecutionContextPool"/> and returns it when the pipeline
/// completes, so a context is only valid for the duration of its dispatch — user code
/// must not hold a reference past the handler's completion. The items dictionary is
/// created lazily on first write and kept (cleared) across reuses; the read paths never
/// allocate it.
/// </remarks>
internal sealed class ErgosfareExecutionContext(
    IDictionary<object, object?>? items, CancellationToken cancellationToken)
    : IExecutionContext, IPoolReturnable
{
    private IDictionary<object, object?>? _items = items;

    /// <summary>
    /// Whether <see cref="_items"/> was created lazily by this context (owned) as opposed
    /// to adopted from the caller (a settings object's dictionary). Owned dictionaries are
    /// cleared and kept across pool reuses for their capacity; adopted ones are detached
    /// untouched on <see cref="Clear"/> — the caller keeps whatever handlers wrote, and the
    /// pool can never hand one dispatch's dictionary to the next.
    /// </summary>
    private bool _ownsItems;

    /// <summary>
    /// Gets the <see cref="CancellationToken"/> associated with the current execution context.
    /// This token can be used to observe cancellation requests and propagate them to handlers or interceptors.
    /// </summary>
    public CancellationToken CancellationToken { get; private set; } = cancellationToken;

    /// <summary>
    /// Gets a dictionary of arbitrary key-value pairs stored in the execution context.
    /// This can be used to share data between different handlers, interceptors, or other pipeline components.
    /// The backing dictionary is created lazily on first access so dispatches that never
    /// touch shared items pay no allocation for it.
    /// </summary>
    public IDictionary<object, object?> Items
    {
        get
        {
            if (_items is null)
            {
                _items = new Dictionary<object, object?>();
                _ownsItems = true;
            }

            return _items;
        }
    }

    /// <summary>Re-initializes a pooled instance for a new dispatch.</summary>
    public void Reset(IDictionary<object, object?>? items, CancellationToken cancellationToken)
    {
        if (items is not null)
        {
            _items = items;
            _ownsItems = false;
        }

        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Clears the per-dispatch state before the instance goes back to the pool. The
    /// (possibly caller-supplied) items dictionary is emptied and kept, so a lazily
    /// created one gets its capacity reused.
    /// </summary>
    public void Clear()
    {
        if (_items is not null)
        {
            if (_ownsItems)
            {
                // Lazily created here: clear and keep, so the capacity is reused.
                _items.Clear();
            }
            else
            {
                // Adopted from the caller: detach without touching its contents — the
                // caller reads results from it, and clearing it here would silently wipe
                // their settings object (and retaining it would leak it into the next
                // dispatch that rents this context).
                _items = null;
            }
        }

        CancellationToken = default;
    }

    /// <inheritdoc />
    public void ReturnToPool() => ErgosfareExecutionContextPool.Return(this);

    /// <inheritdoc />
    public ExecutionContextScope CreateScope()
        => new(ErgosfareExecutionContextPool.Rent(items: null, CancellationToken));

    /// <summary>
    /// Stores an item in the execution context under the specified key.
    /// If an item with the same key already exists, it will be overwritten.
    /// </summary>
    /// <param name="key">The unique key to associate with the item.</param>
    /// <param name="item">The object to store in the context.</param>
    public void Set(string key, object item)
    {
        Items[key] = item;
    }


    /// <summary>
    /// Checks whether an item with the specified key exists in the context.
    /// Never allocates the backing dictionary: an empty context answers <c>false</c>.
    /// </summary>
    /// <param name="key">The key to check for existence.</param>
    /// <returns><c>true</c> if an item with the given key exists; otherwise, <c>false</c>.</returns>
    public bool Has(string key)
    {
        return _items?.ContainsKey(key) ?? false;
    }


    /// <summary>
    /// Retrieves an item of the specified type from the context using the given key.
    /// Never allocates the backing dictionary.
    /// </summary>
    /// <typeparam name="TType">The type of the item to retrieve.</typeparam>
    /// <param name="key">The key associated with the item.</param>
    /// <returns>The item associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no item exists with the specified key.</exception>
    /// <exception cref="InvalidCastException">Thrown if the stored item cannot be cast to <typeparamref name="TType"/>.</exception>
    public TType Get<TType>(string key) where TType : notnull
    {
        if (_items is null || !_items.TryGetValue(key, out var item))
        {
            throw new KeyNotFoundException($"No item with key '{key}' exists in the execution context.");
        }

        return (TType)item!;
    }


    /// <summary>
    /// Attempts to retrieve an item of the specified type from the context using the
    /// given key. Never allocates the backing dictionary.
    /// </summary>
    /// <typeparam name="TType">The type of the item expected.</typeparam>
    /// <param name="key">The key associated with the item.</param>
    /// <param name="item">
    /// When this method returns, contains the retrieved item if found and of the correct type; otherwise, the default value for <typeparamref name="TType"/>.
    /// </param>
    /// <returns><c>true</c> if an item with the given key exists and is of the correct type; otherwise, <c>false</c>.</returns>
    public bool TryGet<TType>(string key, out TType item)
    {
        if (_items is not null && _items.TryGetValue(key, out var el))
        {
            item = (TType)el!;
            return true;
        }

        item = default!;
        return false;
    }


    /// <inheritdoc />
    /// <remarks>
    /// <see cref="ExecutionAbortedException"/> is how the short circuit travels: it unwinds
    /// the participant and everything between it and the mediation strategy (or the baked
    /// plan), which catches it, skips the exception stage and returns the result produced so
    /// far. It is an implementation detail of the unwind and never reaches the caller — a
    /// <c>catch</c> for it in participant code would defeat the abort, not observe it.
    /// </remarks>
    public void Abort()
    {
        throw new ExecutionAbortedException();
    }
}
