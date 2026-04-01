using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class BytesStrategy(int size) : Strategy<byte[]>
{
    internal override byte[] Generate(ConjectureData data) => data.NextBytes(size);
}
