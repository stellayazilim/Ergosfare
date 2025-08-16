namespace Ergosfare.Contracts;


public class ErgoResult<TResult> : ErgoResult, IMessage
{
    public new TResult? Result
    {
        get => (TResult?)base.Result;
        protected set => base.Result = value;
    }

    public ErgoResult<TResult> Success(TResult result)
    {
        Result = result;
        IsSuccess = true;
        return this;
    }

    public override ErgoResult<TResult> Fail(params Exception[]? exceptions)
    {
        foreach (var ex in exceptions ?? Array.Empty<Exception>())
        {
            AddException(ex);
        }
        IsSuccess = false;
        return this;
    }
}


public static class ErgoResultFactories
{
    
    public static ErgoResult Success()
    {
        return new ErgoResult().Success();
    }

    
    public static ErgoResult<TResult> Success<TResult>(TResult result) where TResult : notnull
    {
        return new ErgoResult<TResult>().Success(result);
    }



    public static ErgoResult Fail(params Exception[]? exceptions)
    {
        return new ErgoResult().Fail(exceptions ?? []);
    }


    public static ErgoResult<TResult> Fail<TResult>(params Exception[]? exceptions) where TResult : notnull
    {
        return new ErgoResult<TResult>().Fail(exceptions ?? []);
    }
}