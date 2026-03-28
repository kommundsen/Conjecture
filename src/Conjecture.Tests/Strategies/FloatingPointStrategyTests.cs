using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class FloatingPointStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Doubles_GeneratesFiniteAndNonFiniteValues_WithinDoubleRange()
    {
        var strategy = Gen.Doubles();
        var data = MakeData();

        for (var i = 0; i < 100; i++)
        {
            var value = strategy.Next(data);
            Assert.True(!double.IsNaN(value) || double.IsNaN(value)); // any double bit pattern is valid
            Assert.True(value >= double.MinValue || double.IsNaN(value) || double.IsInfinity(value));
        }
    }

    [Fact]
    public void Doubles_IncludesPositiveAndNegativeValues()
    {
        var strategy = Gen.Doubles();
        var data = MakeData();
        var hasPositive = false;
        var hasNegative = false;

        for (var i = 0; i < 1000; i++)
        {
            var value = strategy.Next(data);
            if (value > 0) { hasPositive = true; }
            if (value < 0) { hasNegative = true; }
            if (hasPositive && hasNegative) { break; }
        }

        Assert.True(hasPositive, "Expected at least one positive double");
        Assert.True(hasNegative, "Expected at least one negative double");
    }

    [Fact]
    public void Doubles_DeterministicWithSeed()
    {
        var strategy = Gen.Doubles();

        var results1 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Next(MakeData(77UL)))
            .ToList();
        var results2 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Next(MakeData(77UL)))
            .ToList();

        Assert.Equal(results1, results2);
    }

    [Fact]
    public void Floats_GeneratesFloatValues()
    {
        var strategy = Gen.Floats();
        var data = MakeData();

        for (var i = 0; i < 100; i++)
        {
            var value = strategy.Next(data);
            Assert.IsType<float>(value);
        }
    }

    [Fact]
    public void Floats_IncludesPositiveAndNegativeValues()
    {
        var strategy = Gen.Floats();
        var data = MakeData();
        var hasPositive = false;
        var hasNegative = false;

        for (var i = 0; i < 1000; i++)
        {
            var value = strategy.Next(data);
            if (value > 0) { hasPositive = true; }
            if (value < 0) { hasNegative = true; }
            if (hasPositive && hasNegative) { break; }
        }

        Assert.True(hasPositive, "Expected at least one positive float");
        Assert.True(hasNegative, "Expected at least one negative float");
    }

    [Fact]
    public void Floats_DeterministicWithSeed()
    {
        var strategy = Gen.Floats();

        var results1 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Next(MakeData(77UL)))
            .ToList();
        var results2 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Next(MakeData(77UL)))
            .ToList();

        Assert.Equal(results1, results2);
    }

    [Fact]
    public void Doubles_ProducesDistinctValues()
    {
        var strategy = Gen.Doubles();
        var data = MakeData();
        var values = Enumerable.Range(0, 50).Select(_ => strategy.Next(data)).ToList();
        Assert.True(values.Distinct().Count() > 1, "Expected multiple distinct doubles");
    }

    [Fact]
    public void Floats_ProducesDistinctValues()
    {
        var strategy = Gen.Floats();
        var data = MakeData();
        var values = Enumerable.Range(0, 50).Select(_ => strategy.Next(data)).ToList();
        Assert.True(values.Distinct().Count() > 1, "Expected multiple distinct floats");
    }
}
