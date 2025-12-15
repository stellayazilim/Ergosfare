using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions;



/// <summary>
/// Represents a strongly-typed snapshot of a computation or intermediate step.
/// Inherits from <see cref="ISnapshot"/> to provide both generic and non-generic result access.
/// </summary>
/// <typeparam name="TResult">
/// The type of the captured result for this snapshot step. 
/// This is not necessarily the final pipeline result.
/// </typeparam>
public interface ISnapshot< TResult>: ISnapshot
{
    /// <inheritdoc />
    object ISnapshot.Result
    {
        get => Result; set  => Result = (TResult)value;
    }
    
    /// <summary>
    /// Gets the strongly-typed result of the snapshot step.
    /// </summary>
    public new TResult Result { get; internal set; }
}
                    
