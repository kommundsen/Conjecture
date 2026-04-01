using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class SetStrategy<T>(Strategy<T> inner, int minSize, int maxSize) : Strategy<IReadOnlySet<T>>
{
    private const int MaxAttemptsPerElement = 200;
    private readonly ulong ulongMinSize = (ulong)minSize;
    private readonly ulong ulongMaxSize = (ulong)maxSize;

    internal override IReadOnlySet<T> Generate(ConjectureData data)
    {
        var size = (int)data.NextInteger(ulongMinSize, ulongMaxSize);
        var set = new HashSet<T>(size);
        for (var i = 0; i < size; i++)
        {
            int attempt;
            for (attempt = 0; attempt < MaxAttemptsPerElement; attempt++)
            {
                if (set.Add(inner.Generate(data)))
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
