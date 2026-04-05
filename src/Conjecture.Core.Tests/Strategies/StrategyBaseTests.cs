// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class StrategyBaseTests
{
    private sealed class ConstantStrategy<T>(T value) : Strategy<T>
    {
        internal override T Generate(ConjectureData data) => value;
    }

    private sealed class TrackingStrategy : Strategy<int>
    {
        public bool WasCalled { get; private set; }
        internal override int Generate(ConjectureData data) { WasCalled = true; return 0; }
    }

    private static ConjectureData MakeData() =>
        ConjectureData.ForGeneration(new SplittableRandom(1UL));

    [Fact]
    public void Strategy_IsAbstract()
    {
        Assert.True(typeof(Strategy<int>).IsAbstract);
    }

    [Fact]
    public void Next_IsCalled_WithConjectureData()
    {
        var strategy = new TrackingStrategy();
        strategy.Generate(MakeData());
        Assert.True(strategy.WasCalled);
    }

    [Fact]
    public void Next_ReturnsSubclassValue()
    {
        var strategy = new ConstantStrategy<string>("hello");
        var result = strategy.Generate(MakeData());
        Assert.Equal("hello", result);
    }
}