using Stella.Ergosfare.Core.Abstractions.Exceptions;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The execution context for message handling and mediation: contextual information such as
/// the cancellation token and per-dispatch items, plus control over pipeline execution
/// (scoping and abort). Handlers and interceptors receive it as their last parameter.
/// </summary>
/// <remarks>
/// <para>
/// The type is sealed and taken concretely by every handler and interceptor contract, so member
/// access is a direct call — there is no interface to dispatch through.
/// </para>
/// <para>
/// Instances are pooled: a dispatch rents one from <see cref="ErgosfareContextPool"/> and returns
/// it when the pipeline completes, so a context is only valid for the duration of its dispatch —
/// user code must not hold a reference past the handler's completion. The items dictionary is
/// created lazily on first write and kept (cleared) across reuses; the read paths never allocate
/// it.
/// </para>
/// <para>
/// A context constructed directly (rather than rented) is never pooled and costs exactly what an
/// unpooled context did; this is how tests and callers that want to own the items dictionary
/// build one.
/// </para>
/// </remarks>
public sealed class ErgosfareContext(
    IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default)
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
    /// Gets the cancellation token associated with the execution context.
    /// Handlers should periodically check this token and abort execution if cancellation is
    /// requested, and propagate it to any work they start.
    /// </summary>
    public CancellationToken CancellationToken { get; private set; } = cancellationToken;

    /// <summary>
    /// Gets a key/value collection for sharing data within the scope of this execution.
    /// Data is scoped to the current execution and is not shared across different mediation
    /// operations. The backing dictionary is created lazily on first access so dispatches that
    /// never touch shared items pay no allocation for it.
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
    /// Clears the per-dispatch state before the instance goes back to the pool. The
    /// (possibly caller-supplied) items dictionary is emptied and kept, so a lazily
    /// created one gets its capacity reused.
    /// </summary>
    internal void Clear()
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

    /// <summary>Returns this context to the pool. Called by the scope on dispose.</summary>
    internal void ReturnToPool() => ErgosfareContextPool.Return(this);

    /// <summary>
    /// Opens a child execution-context scope for a nested mediator call: the child starts
    /// with clean items and inherits this context's cancellation token. Dispose the scope
    /// when the nested call completes; the child must not be used afterwards.
    /// </summary>
    /// <example>
    /// <code>
    /// using var scope = context.CreateScope();
    /// await commandMediator.SendAsync(new InnerCommand(), scope.Context);
    /// </code>
    /// </example>
    public ErgosfareContextScope CreateScope()
        => new(ErgosfareContextPool.Rent(items: null, CancellationToken));

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


    /// <summary>
    /// Ends the current mediation: nothing after the calling participant runs, and the
    /// caller is told, by <see cref="ExecutionAbortedException"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dispatch was asked for by the call site, so the call site is who hears that it
    /// did not happen — a pipeline whose participants can abort is one the caller wraps in
    /// a <c>try</c>. The alternative, returning the result type's default, is
    /// indistinguishable from a handler that legitimately produced nothing.
    /// </para>
    /// <para>
    /// Stopping means stopping: nothing downstream runs. Not the rest of the current stage,
    /// not the exception stage — an abort is not a failure and exception interceptors exist
    /// to handle failures — and not the final stage either. There is no result to expect
    /// from a pipeline that was cut, which is why the signal carries none.
    /// </para>
    /// <para>
    /// Nothing catches this on the way out. The stages that do have exception handling —
    /// the strategies and the emitted plans — filter it through untouched and skip their
    /// own remaining work, so what the caller receives is the participant's own signal with
    /// its stack intact.
    /// </para>
    /// </remarks>
    /// <exception cref="ExecutionAbortedException">Always — this is how the abort travels.</exception>
    public void Abort() => throw new ExecutionAbortedException();

    /// <summary>
    /// Stops the pipeline, saying why. See <see cref="Abort()"/>.
    /// </summary>
    /// <param name="reason">
    /// Why the pipeline is being stopped; arrives on
    /// <see cref="ExecutionAbortedException.Reason"/>.
    /// </param>
    /// <remarks><inheritdoc cref="Abort()" path="/remarks"/></remarks>
    /// <exception cref="ExecutionAbortedException">Always — this is how the abort travels.</exception>
    public void Abort(string? reason) => throw new ExecutionAbortedException(reason);

    /// <summary>
    /// Stops the pipeline, saying why and handing the caller something to act on. See
    /// <see cref="Abort()"/>.
    /// </summary>
    /// <param name="reason">
    /// Why the pipeline is being stopped; arrives on
    /// <see cref="ExecutionAbortedException.Reason"/>.
    /// </param>
    /// <param name="value">
    /// What the caller should act on; arrives on
    /// <see cref="ExecutionAbortedException.Value"/>.
    /// </param>
    /// <remarks><inheritdoc cref="Abort()" path="/remarks"/></remarks>
    /// <exception cref="ExecutionAbortedException">Always — this is how the abort travels.</exception>
    public void Abort(string? reason, object? value) => throw new ExecutionAbortedException(reason, value);
}
