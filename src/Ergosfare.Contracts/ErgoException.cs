namespace Ergosfare.Contracts;

public class ErgoException(IList<Exception> exceptions): Exception(exceptions.FirstOrDefault()?.Message, innerException:  exceptions.FirstOrDefault())
{
    public IList<Exception> Exceptions { get; init; } = exceptions;
}