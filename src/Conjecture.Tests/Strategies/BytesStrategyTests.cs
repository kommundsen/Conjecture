using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Generation;

public class BytesStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Bytes_ReturnsArrayOfCorrectLength()
    {
        var result = Gen.Bytes(8).Next(MakeData());
        Assert.Equal(8, result.Length);
    }

    [Fact]
    public void Bytes_ZeroLength_ReturnsEmptyArray()
    {
        var result = Gen.Bytes(0).Next(MakeData());
        Assert.Empty(result);
    }

    [Fact]
    public void Bytes_RecordsIRNode()
    {
        var data = MakeData();
        Gen.Bytes(8).Next(data);
        var node = Assert.Single(data.IRNodes);
        Assert.Equal(IRNodeKind.Bytes, node.Kind);
    }

    [Fact]
    public void Bytes_ProducesNonZeroContent()
    {
        var strategy = Gen.Bytes(16);
        var data = MakeData();
        var anyNonZero = false;
        for (var i = 0; i < 10; i++)
        {
            var bytes = strategy.Next(data);
            if (Array.Exists(bytes, b => b != 0)) { anyNonZero = true; break; }
        }
        Assert.True(anyNonZero, "Bytes() produced all-zero arrays across 10 draws.");
    }
}
