namespace Ergosfare.Contracts;

public class ErgoResult
{
    private readonly IList<Exception> _errors = new List<Exception>();
    public IReadOnlyList<Exception> Errors => _errors.ToList().AsReadOnly();

    protected void AddException(Exception exception)
    {
        _errors.Add(exception);
    }
    
    public object? Result { get; protected set; }

    public bool IsSuccess { get; protected set; } = true;
   
    public ErgoResult Success()
    {
        IsSuccess = true;
        return this;
    }

    public virtual ErgoResult Fail(params Exception[]? exceptions)
    {
        foreach (var ex in exceptions ?? Array.Empty<Exception>())
        {
            AddException(ex);
        }

        IsSuccess = false;
        
        return this;
    }
}

