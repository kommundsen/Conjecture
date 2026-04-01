using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class BytesStrategy : Strategy<byte[]>
{
    private readonly int size;

    internal BytesStrategy(int size) => this.size = size;

    internal override byte[] Generate(ConjectureData data) => data.NextBytes(size);
}
