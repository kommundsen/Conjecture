using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class ListStrategy<T> : Strategy<List<T>>
{
    private readonly Strategy<T> elementStrategy;
    private readonly ulong minLength;
    private readonly ulong maxLength;

    internal ListStrategy(Strategy<T> elementStrategy, int minLength, int maxLength)
    {
        this.elementStrategy = elementStrategy;
        this.minLength = (ulong)minLength;
        this.maxLength = (ulong)maxLength;
    }

    internal override List<T> Next(ConjectureData data)
    {
        var length = (int)data.DrawInteger(minLength, maxLength);
        var result = new List<T>(length);
        for (var i = 0; i < length; i++)
        {
            result.Add(elementStrategy.Next(data));
        }
        return result;
    }
}
