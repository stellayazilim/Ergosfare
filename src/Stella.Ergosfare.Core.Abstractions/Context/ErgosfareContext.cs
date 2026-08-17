using Stella.Ergosfare.Core.Abstractions.Exceptions;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The execution context of one dispatch: the cancellation token, the items participants
/// share with each other, and the means to open a nested scope or stop the pipeline. Every
/// handler and interceptor receives it as its last parameter.
/// </summary>
/// <remarks>
/// <para>
/// A context is valid only for the dispatch it belongs to. Dispatches rent contexts from a
/// pool and return them once the pipeline completes, so holding a reference past the
/// handler's completion observes another dispatch's state. A context constructed directly
/// is never pooled, which is how a caller that wants to keep the items dictionary builds
/// one.
/// </para>
/// <para>
/// The items dictionary is allocated on first write. Every read path — <see cref="Has"/>,
/// <see cref="Get{TType}"/>, <see cref="TryGet{TType}"/> — leaves it unallocated.
/// </para>
/// </remarks>
public sealed class ErgosfareContext(
    IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default)
{
    private IDictionary<object, object?>? _items = items;

    /// <summary>
    /// Whether the items dictionary was allocated by this context rather than supplied by
    /// the caller. An owned dictionary is cleared and kept for its capacity when the
    /// context is recycled; a supplied one is detached untouched, so the caller keeps what
    /// participants wrote into it and the next dispatch never sees it.
    /// </summary>
    private bool _ownsItems;

    /// <summary>
    /// The cancellation token for this dispatch. Handlers should observe it and pass it to
    /// any work they start.
    /// </summary>
    public CancellationToken CancellationToken { get; private set; } = cancellationToken;

    /// <summary>
    /// The items shared between the participants of this dispatch. The dictionary is
    /// allocated on first access and belongs to this dispatch alone.
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

    /// <summary>
    /// Prepares a recycled instance for a new dispatch.
    /// </summary>
    /// <param name="items">The caller's items dictionary, or <c>null</c> to allocate on demand.</param>
    /// <param name="cancellationToken">The token for the new dispatch.</param>
    internal void Reset(IDictionary<object, object?>? items, CancellationToken cancellationToken)
    {
        if (items is not null)
        {
            _items = items;
            _ownsItems = false;
        }

        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Drops this dispatch's state so the instance can be recycled.
    /// </summary>
    internal void Clear()
    {
        if (_items is not null)
        {
            if (_ownsItems)
            {
                // Allocated here, so it is emptied and kept — the next dispatch reuses its
                // capacity.
                _items.Clear();
            }
            else
            {
                // Supplied by the caller, who reads results out of it: detach without
                // touching the contents, which also keeps it out of the next dispatch.
                _items = null;
            }
        }

        CancellationToken = default;
    }

    /// <summary>
    /// Hands this context back for recycling. Called by the scope on dispose.
    /// </summary>
    internal void ReturnToPool() => ErgosfareContextPool.Return(this);

    /// <summary>
    /// Opens a child context for a nested dispatch. The child starts with no items and
    /// inherits this context's cancellation token, keeping nested work on the same
    /// cancellation chain.
    /// </summary>
    /// <returns>
    /// A scope holding the child context. Dispose it when the nested dispatch completes;
    /// the child must not be used afterwards.
    /// </returns>
    /// <example>
    /// <code>
    /// using var scope = context.CreateScope();
    /// await commandMediator.SendAsync(new InnerCommand(), scope.Context);
    /// </code>
    /// </example>
    public ErgosfareContextScope CreateScope()
        => new(ErgosfareContextPool.Rent(items: null, CancellationToken));

    /// <summary>
    /// Stores <paramref name="item"/> under <paramref name="key"/>, replacing whatever was
    /// stored under that key.
    /// </summary>
    /// <param name="key">The key to store under.</param>
    /// <param name="item">The value to store.</param>
    public void Set(string key, object item)
    {
        Items[key] = item;
    }


    /// <summary>
    /// Reports whether an item is stored under <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to look for.</param>
    /// <returns><c>true</c> when an item is stored under that key.</returns>
    public bool Has(string key)
    {
        return _items?.ContainsKey(key) ?? false;
    }


    /// <summary>
    /// Returns the item stored under <paramref name="key"/>, cast to
    /// <typeparamref name="TType"/>.
    /// </summary>
    /// <typeparam name="TType">The type to cast the stored item to.</typeparam>
    /// <param name="key">The key to read.</param>
    /// <returns>The stored item.</returns>
    /// <exception cref="KeyNotFoundException">Nothing is stored under <paramref name="key"/>.</exception>
    /// <exception cref="InvalidCastException">
    /// The stored item is not a <typeparamref name="TType"/>.
    /// </exception>
    public TType Get<TType>(string key) where TType : notnull
    {
        if (_items is null || !_items.TryGetValue(key, out var item))
        {
            throw new KeyNotFoundException($"No item with key '{key}' exists in the execution context.");
        }

        return (TType)item!;
    }


    /// <summary>
    /// Reads the item stored under <paramref name="key"/> when there is one.
    /// </summary>
    /// <typeparam name="TType">The type to cast the stored item to.</typeparam>
    /// <param name="key">The key to read.</param>
    /// <param name="item">
    /// The stored item when this method returns <c>true</c>; otherwise the default value of
    /// <typeparamref name="TType"/>.
    /// </param>
    /// <returns><c>true</c> when an item is stored under that key.</returns>
    /// <exception cref="InvalidCastException">
    /// An item is stored under <paramref name="key"/> but is not a
    /// <typeparamref name="TType"/>. A stored item of the wrong type is a failure, not a
    /// miss — this method returns <c>false</c> only when the key is absent.
    /// </exception>
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


    /// <summary>
    /// Ends the dispatch: nothing after the calling participant runs, and the caller is
    /// told by <see cref="ExecutionAbortedException"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stopping stops everything downstream — the rest of the current stage, the exception
    /// stage, and the final stage alike. An abort is not a failure, so the exception
    /// interceptors do not see it, and there is no result for the pipeline to produce,
    /// which is why the signal carries none.
    /// </para>
    /// <para>
    /// Nothing swallows the signal on its way out: the strategies and generated plans let
    /// it pass through their own exception handling untouched, so the caller receives the
    /// participant's signal with its original stack. Callers that dispatch abortable
    /// pipelines should expect it.
    /// </para>
    /// </remarks>
    /// <exception cref="ExecutionAbortedException">Always; this is how the abort travels.</exception>
    public void Abort() => throw new ExecutionAbortedException();

    /// <summary>
    /// Ends the dispatch, recording why. See <see cref="Abort()"/>.
    /// </summary>
    /// <param name="reason">
    /// Why the dispatch is ending; arrives on <see cref="ExecutionAbortedException.Reason"/>.
    /// </param>
    /// <remarks><inheritdoc cref="Abort()" path="/remarks"/></remarks>
    /// <exception cref="ExecutionAbortedException">Always; this is how the abort travels.</exception>
    public void Abort(string? reason) => throw new ExecutionAbortedException(reason);

    /// <summary>
    /// Ends the dispatch, recording why and handing the caller a value to act on. See
    /// <see cref="Abort()"/>.
    /// </summary>
    /// <param name="reason">
    /// Why the dispatch is ending; arrives on <see cref="ExecutionAbortedException.Reason"/>.
    /// </param>
    /// <param name="value">
    /// The value for the caller to act on; arrives on
    /// <see cref="ExecutionAbortedException.Value"/>.
    /// </param>
    /// <remarks><inheritdoc cref="Abort()" path="/remarks"/></remarks>
    /// <exception cref="ExecutionAbortedException">Always; this is how the abort travels.</exception>
    public void Abort(string? reason, object? value) => throw new ExecutionAbortedException(reason, value);
}
