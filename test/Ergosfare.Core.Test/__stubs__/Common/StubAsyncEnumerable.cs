using System.Runtime.CompilerServices;

namespace Ergosfare.Core.Test.__stubs__;

internal static class StubAsyncEnumerable
{
    public static async IAsyncEnumerable<T> From<T>(
        IEnumerable<T> items,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item; // ✅ must yield
            await Task.Yield(); // simulate async
        }
    }

    public static IAsyncEnumerable<T> From<T>(params T[] items)
    {
        return From((IEnumerable<T>)items);
    }
}