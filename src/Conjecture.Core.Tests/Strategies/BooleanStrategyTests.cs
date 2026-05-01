// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class BooleanStrategyTests
{
    [Fact]
    public void Booleans_ReturnsStrategy()
    {
        Strategy<bool> strategy = Strategy.Booleans();
        Assert.NotNull(strategy);
        Assert.IsAssignableFrom<Strategy<bool>>(strategy);
    }

    [Fact]
    public void Booleans_ReturnsBothValues()
    {
        Strategy<bool> strategy = Strategy.Booleans();
        IReadOnlyList<bool> values = strategy.WithSeed(42UL).Sample(1000);
        Assert.Contains(values, v => v);
        Assert.Contains(values, v => !v);
    }

    [Fact]
    public void BooleanStrategy_Next_RecordsIRNode()
    {
        Strategy<bool> strategy = Strategy.Booleans();
        ConjectureData data = ConjectureData.ForGeneration(new SplittableRandom(42UL));

        strategy.Generate(data);

        IRNode node = Assert.Single(data.IRNodes);
        Assert.Equal(IRNodeKind.Boolean, node.Kind);
    }
}
