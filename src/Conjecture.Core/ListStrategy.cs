using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class ListStrategy<T> : Strategy<List<T>>
{
    private readonly Strategy<T> inner;
    private readonly ulong minSize;
    private readonly ulong maxSize;

    internal ListStrategy(Strategy<T> inner, int minSize, int maxSize)
    {
        this.inner = inner;
        this.minSize = (ulong)minSize;
        this.maxSize = (ulong)maxSize;
    }

    internal override List<T> Generate(ConjectureData data)
    {
        var size = (int)data.NextInteger(minSize, maxSize);
        var list = new List<T>(size);
        for (var i = 0; i < size; i++)
        {
            list.Add(inner.Generate(data));
        }
        return list;
    }
}
