using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class DictionaryStrategy<TKey, TValue> : Strategy<IReadOnlyDictionary<TKey, TValue>>
    where TKey : notnull
{
    private const int MaxAttemptsPerElement = 200;
    private readonly Strategy<TKey> keyStrategy;
    private readonly Strategy<TValue> valueStrategy;
    private readonly ulong minSize;
    private readonly ulong maxSize;

    internal DictionaryStrategy(Strategy<TKey> keyStrategy, Strategy<TValue> valueStrategy, int minSize, int maxSize)
    {
        this.keyStrategy = keyStrategy;
        this.valueStrategy = valueStrategy;
        this.minSize = (ulong)minSize;
        this.maxSize = (ulong)maxSize;
    }

    internal override IReadOnlyDictionary<TKey, TValue> Next(ConjectureData data)
    {
        var size = (int)data.DrawInteger(minSize, maxSize);
        var dict = new Dictionary<TKey, TValue>(size);
        for (var i = 0; i < size; i++)
        {
            int attempt;
            for (attempt = 0; attempt < MaxAttemptsPerElement; attempt++)
            {
                var key = keyStrategy.Next(data);
                if (!dict.ContainsKey(key))
                {
                    dict[key] = valueStrategy.Next(data);
                    break;
                }
            }
            if (attempt == MaxAttemptsPerElement)
            {
                data.MarkInvalid();
                throw new UnsatisfiedAssumptionException();
            }
        }
        return dict;
    }
}
