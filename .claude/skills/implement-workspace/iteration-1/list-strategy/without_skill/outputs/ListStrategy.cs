using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class ListStrategy<T> : Strategy<List<T>>
{
    private readonly Strategy<T> elementStrategy;
    private readonly int minLength;
    private readonly int maxLength;

    internal ListStrategy(Strategy<T> elementStrategy, int minLength, int maxLength)
    {
        this.elementStrategy = elementStrategy;
        this.minLength = minLength;
        this.maxLength = maxLength;
    }

    internal override List<T> Next(ConjectureData data)
    {
        var length = minLength == maxLength
            ? minLength
            : (int)data.DrawInteger((ulong)minLength, (ulong)maxLength);

        var list = new List<T>(length);
        for (var i = 0; i < length; i++)
        {
            list.Add(elementStrategy.Next(data));
        }

        return list;
    }
}
