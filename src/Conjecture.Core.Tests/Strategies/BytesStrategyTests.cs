// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class BytesStrategyTests
{
    [Fact]
    public void Bytes_ReturnsArrayOfCorrectLength()
    {
        byte[] result = Strategy.Bytes(8).Sample();
        Assert.Equal(8, result.Length);
    }

    [Fact]
    public void Bytes_ZeroLength_ReturnsEmptyArray()
    {
        byte[] result = Strategy.Bytes(0).Sample();
        Assert.Empty(result);
    }

    [Fact]
    public void Bytes_RecordsIRNode()
    {
        ConjectureData data = ConjectureData.ForGeneration(new SplittableRandom(42UL));
        Strategy.Bytes(8).Generate(data);
        IRNode node = Assert.Single(data.IRNodes);
        Assert.Equal(IRNodeKind.Bytes, node.Kind);
    }

    [Fact]
    public void Bytes_ProducesNonZeroContent()
    {
        Strategy<byte[]> strategy = Strategy.Bytes(16);
        IReadOnlyList<byte[]> samples = strategy.WithSeed(42UL).Sample(10);
        Assert.Contains(samples, bytes => Array.Exists(bytes, b => b != 0));
    }
}
