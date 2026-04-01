using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class DictionaryStrategy<TKey, TValue>(
    Strategy<TKey> keyStrategy, Strategy<TValue> valueStrategy, int minSize, int maxSize)
    : Strategy<IReadOnlyDictionary<TKey, TValue>>
    where TKey : notnull
{
    private const int MaxAttemptsPerElement = 200;
    private readonly ulong ulongMinSize = (ulong)minSize;
    private readonly ulong ulongMaxSize = (ulong)maxSize;

    internal override IReadOnlyDictionary<TKey, TValue> Generate(ConjectureData data)
    {
        var size = (int)data.NextInteger(ulongMinSize, ulongMaxSize);
        var dict = new Dictionary<TKey, TValue>(size);
        for (var i = 0; i < size; i++)
        {
            int attempt;
            for (attempt = 0; attempt < MaxAttemptsPerElement; attempt++)
            {
                var key = keyStrategy.Generate(data);
                if (!dict.ContainsKey(key))
                {
                    dict[key] = valueStrategy.Generate(data);
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
