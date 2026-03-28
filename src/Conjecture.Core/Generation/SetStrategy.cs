using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class SetStrategy<T> : Strategy<IReadOnlySet<T>>
{
    private const int MaxAttemptsPerElement = 200;
    private readonly Strategy<T> inner;
    private readonly ulong minSize;
    private readonly ulong maxSize;

    internal SetStrategy(Strategy<T> inner, int minSize, int maxSize)
    {
        this.inner = inner;
        this.minSize = (ulong)minSize;
        this.maxSize = (ulong)maxSize;
    }

    internal override IReadOnlySet<T> Next(ConjectureData data)
    {
        var size = (int)data.DrawInteger(minSize, maxSize);
        var set = new HashSet<T>(size);
        for (var i = 0; i < size; i++)
        {
            int attempt;
            for (attempt = 0; attempt < MaxAttemptsPerElement; attempt++)
            {
                if (set.Add(inner.Next(data)))
                {
                    break;
                }
            }
            if (attempt == MaxAttemptsPerElement)
            {
                data.MarkInvalid();
                throw new UnsatisfiedAssumptionException();
            }
        }
        return set;
    }
}
