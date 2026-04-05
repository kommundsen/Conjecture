// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class BytesStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Bytes_ReturnsArrayOfCorrectLength()
    {
        var result = Generate.Bytes(8).Generate(MakeData());
        Assert.Equal(8, result.Length);
    }

    [Fact]
    public void Bytes_ZeroLength_ReturnsEmptyArray()
    {
        var result = Generate.Bytes(0).Generate(MakeData());
        Assert.Empty(result);
    }

    [Fact]
    public void Bytes_RecordsIRNode()
    {
        var data = MakeData();
        Generate.Bytes(8).Generate(data);
        var node = Assert.Single(data.IRNodes);
        Assert.Equal(IRNodeKind.Bytes, node.Kind);
    }

    [Fact]
    public void Bytes_ProducesNonZeroContent()
    {
        var strategy = Generate.Bytes(16);
        var data = MakeData();
        var anyNonZero = false;
        for (var i = 0; i < 10; i++)
        {
            var bytes = strategy.Generate(data);
            if (Array.Exists(bytes, b => b != 0)) { anyNonZero = true; break; }
        }
        Assert.True(anyNonZero, "Bytes() produced all-zero arrays across 10 draws.");
    }
}