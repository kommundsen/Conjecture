using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class SelectStrategy<TSource, TResult>(Strategy<TSource> source, Func<TSource, TResult> selector) : Strategy<TResult>
{
    internal override TResult Generate(ConjectureData data) => selector(source.Generate(data));
}
