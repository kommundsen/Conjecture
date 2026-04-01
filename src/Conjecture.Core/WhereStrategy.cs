using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class WhereStrategy<T>(Strategy<T> source, Func<T, bool> predicate) : Strategy<T>
{
    private const int MaxAttempts = 200;

    internal override T Generate(ConjectureData data)
    {
        for (var i = 0; i < MaxAttempts; i++)
        {
            var value = source.Generate(data);
            if (predicate(value))
            {
                return value;
            }

        }
        data.MarkInvalid();
        throw new UnsatisfiedAssumptionException();
    }
}
