using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class ZipStrategy<TFirst, TSecond, TResult>(
    Strategy<TFirst> first,
    Strategy<TSecond> second,
    Func<TFirst, TSecond, TResult> resultSelector) : Strategy<TResult>
{
    internal override TResult Generate(ConjectureData data) =>
        resultSelector(first.Generate(data), second.Generate(data));
}
