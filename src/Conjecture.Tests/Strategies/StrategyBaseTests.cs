using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class StrategyBaseTests
{
    private sealed class ConstantStrategy<T>(T value) : Strategy<T>
    {
        internal override T Next(ConjectureData data) => value;
    }

    private sealed class TrackingStrategy : Strategy<int>
    {
        public bool WasCalled { get; private set; }
        internal override int Next(ConjectureData data) { WasCalled = true; return 0; }
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
        strategy.Next(MakeData());
        Assert.True(strategy.WasCalled);
    }

    [Fact]
    public void Next_ReturnsSubclassValue()
    {
        var strategy = new ConstantStrategy<string>("hello");
        var result = strategy.Next(MakeData());
        Assert.Equal("hello", result);
    }
}
