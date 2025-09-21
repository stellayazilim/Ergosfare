using System.Threading.Tasks;

namespace Ergosfare.Core.Abstractions;




public interface ISnapshotService
{
    Task<TResult> Snapshot<TResult>(string name, ISnapshot<TResult> snapshot);
}

