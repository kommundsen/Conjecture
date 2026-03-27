using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class BytesStrategy : Strategy<byte[]>
{
    private readonly int size;

    internal BytesStrategy(int size) => this.size = size;

    internal override byte[] Next(ConjectureData data) => data.DrawBytes(size);
}
