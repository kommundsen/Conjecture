using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class BytesStrategy : Strategy<byte[]>
{
    private readonly int _size;

    internal BytesStrategy(int size) => _size = size;

    internal override byte[] Next(ConjectureData data) => data.DrawBytes(_size);
}
