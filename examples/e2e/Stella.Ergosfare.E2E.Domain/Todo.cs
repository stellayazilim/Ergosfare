namespace Stella.Ergosfare.E2E.Domain;

/// <summary>A single todo item — the one aggregate this e2e app persists.</summary>
public sealed class Todo : Entity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Marks the todo complete and raises a <see cref="TodoCompleted"/> domain event —
    /// a POCO the infrastructure publishes on commit.
    /// </summary>
    public void Complete()
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Raise(new TodoCompleted(Id));
    }
}
