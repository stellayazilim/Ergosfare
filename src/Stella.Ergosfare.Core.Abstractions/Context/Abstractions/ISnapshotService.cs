using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions;




public interface ISnapshotService
{
    Task<TResult> Snapshot<TResult>(string name, ISnapshot<TResult> snapshot);
}

