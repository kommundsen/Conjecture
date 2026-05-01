// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class ArrayStrategyTests
{
    [Fact]
    public void Arrays_FixedSize_ReturnsArrayOfCorrectLength()
    {
        byte[] result = Strategy.Arrays(Strategy.Integers<byte>(), 8, 8).Sample();
        Assert.Equal(8, result.Length);
    }

    [Fact]
    public void Arrays_ZeroLength_ReturnsEmptyArray()
    {
        byte[] result = Strategy.Arrays(Strategy.Integers<byte>(), 0, 0).Sample();
        Assert.Empty(result);
    }

    [Fact]
    public void Arrays_VariableSize_StaysWithinBounds()
    {
        Strategy<byte[]> strategy = Strategy.Arrays(Strategy.Integers<byte>(), 2, 8);
        Assert.All(strategy.WithSeed(42UL).Sample(50), arr => Assert.InRange(arr.Length, 2, 8));
    }

    [Fact]
    public void Arrays_ProducesNonZeroContent()
    {
        Strategy<byte[]> strategy = Strategy.Arrays(Strategy.Integers<byte>(), 16, 16);
        IReadOnlyList<byte[]> samples = strategy.WithSeed(42UL).Sample(10);
        Assert.Contains(samples, bytes => Array.Exists(bytes, b => b != 0));
    }

    [Fact]
    public void Arrays_OfInt_ReturnsCorrectShape()
    {
        Strategy<int[]> strategy = Strategy.Arrays(Strategy.Integers<int>(0, 100), 3, 3);
        int[] result = strategy.WithSeed(1UL).Sample();
        Assert.Equal(3, result.Length);
        Assert.All(result, v => Assert.InRange(v, 0, 100));
    }

    [Fact]
    public void Arrays_RecordsLengthAndPerElementNodes()
    {
        ConjectureData data = ConjectureData.ForGeneration(new SplittableRandom(42UL));
        Strategy.Arrays(Strategy.Integers<byte>(), 4, 4).Generate(data);
        Assert.Equal(5, data.IRNodes.Count);
        Assert.All(data.IRNodes, n => Assert.Equal(IRNodeKind.Integer, n.Kind));
    }
}
