using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Tests;

public class StrategyProviderTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    private sealed class PositiveIntsProvider : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Gen.Integers<int>(1, int.MaxValue);
    }

    private sealed class EvenIntsProvider : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Gen.Integers<int>(0, 50).Where(n => n % 2 == 0);
    }

    [Fact]
    public void Create_ReturnsWorkingStrategy()
    {
        IStrategyProvider<int> provider = new PositiveIntsProvider();

        Strategy<int> strategy = provider.Create();

        Assert.NotNull(strategy);
    }

    [Fact]
    public void Create_ReturnedStrategy_GeneratesValuesInRange()
    {
        IStrategyProvider<int> provider = new PositiveIntsProvider();
        Strategy<int> strategy = provider.Create();
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            int value = strategy.Next(data);
            Assert.True(value >= 1, $"Expected positive value, got {value}");
        }
    }

    [Fact]
    public void Strategy_FromProvider_PassesTestRunner()
    {
        IStrategyProvider<int> provider = new PositiveIntsProvider();
        Strategy<int> strategy = provider.Create();
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };

        TestRunResult result = TestRunner.Run(settings, data =>
        {
            int value = strategy.Next(data);
            if (value <= 0)
            {
                throw new InvalidOperationException($"Expected positive, got {value}");
            }
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public void Create_ComposedWithWhere_GeneratesOnlyMatchingValues()
    {
        IStrategyProvider<int> provider = new EvenIntsProvider();
        Strategy<int> strategy = provider.Create();
        ConjectureData data = MakeData();

        for (int i = 0; i < 50; i++)
        {
            int value = strategy.Next(data);
            Assert.Equal(0, value % 2);
        }
    }
}
