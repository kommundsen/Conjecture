using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class StringShrinkingTests
{
    // --- Shrinks toward shorter strings ---

    [Fact]
    public void Strings_FailingProperty_ShrinksToPreciseMinimumLength()
    {
        // Property fails when string length >= 3.
        // Shrunk string must have length exactly 3 (shortest failing string).
        var strategy = Gen.Strings(minLength: 0, maxLength: 20);
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            var s = strategy.Next(data);
            if (s.Length >= 3) { throw new Exception("too long"); }
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var shrunk = strategy.Next(replay);
        Assert.Equal(3, shrunk.Length);
    }

    [Fact]
    public void Strings_FailingProperty_ShrinksToEmptyString_WhenAnyLengthFails()
    {
        // Property fails for any non-empty string.
        // Minimum failing example is the empty string — length 0 doesn't fail,
        // so minimum is length 1. This verifies the shrinker reduces length as far as possible.
        var strategy = Gen.Strings(minLength: 0, maxLength: 10);
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 2UL };

        var result = TestRunner.Run(settings, data =>
        {
            var s = strategy.Next(data);
            if (s.Length > 0) { throw new Exception("non-empty"); }
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var shrunk = strategy.Next(replay);
        Assert.Equal(1, shrunk.Length);
    }

    // --- Shrinks toward earlier alphabet characters ---

    [Fact]
    public void Strings_FailingProperty_ShrinksTowardEarliestFailingChar()
    {
        // Using alphabet "abcde...z", property fails when string length >= 1.
        // Shrunk string should be a single character. The shrinker will also reduce
        // the character code toward the minimum (first char in alphabet = 'a'),
        // but 'a' does not make the property fail any differently — so the shrunk
        // string should be a single character that is as early in the alphabet as possible.
        var strategy = Gen.Strings(alphabet: "abcdefghijklmnopqrstuvwxyz", minLength: 0, maxLength: 10);
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 3UL };

        var result = TestRunner.Run(settings, data =>
        {
            var s = strategy.Next(data);
            if (s.Length >= 1) { throw new Exception("non-empty"); }
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var shrunk = strategy.Next(replay);
        Assert.Equal(1, shrunk.Length);
        Assert.Equal('a', shrunk[0]);
    }

    [Fact]
    public void Strings_FailingProperty_ShrinksToMinimumFailingChar()
    {
        // Property fails when string contains a char >= 'e'.
        // Shrunk string should be exactly "e" (length 1, earliest failing char).
        var strategy = Gen.Strings(alphabet: "abcdefghijklmnopqrstuvwxyz", minLength: 0, maxLength: 20);
        var settings = new ConjectureSettings { MaxExamples = 500, Seed = 5UL };

        var result = TestRunner.Run(settings, data =>
        {
            var s = strategy.Next(data);
            if (s.Any(c => c >= 'e')) { throw new Exception("char too late"); }
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var shrunk = strategy.Next(replay);
        Assert.Equal(1, shrunk.Length);
        Assert.Equal('e', shrunk[0]);
    }

    // --- Shrunk string still satisfies the failure condition ---

    [Fact]
    public void Strings_ShrunkCounterexample_StillSatisfiesFailureCondition()
    {
        // Whatever the shrinker produces, replaying the counterexample must still
        // trigger the original exception.
        var strategy = Gen.Strings(minLength: 0, maxLength: 15);
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 7UL };
        Exception? captured = null;

        var result = TestRunner.Run(settings, data =>
        {
            var s = strategy.Next(data);
            if (s.Length >= 4) { throw new InvalidOperationException($"too long: '{s}'"); }
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        try
        {
            var shrunk = strategy.Next(replay);
            if (shrunk.Length >= 4) { throw new InvalidOperationException($"too long: '{shrunk}'"); }
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        Assert.NotNull(captured);
        Assert.IsType<InvalidOperationException>(captured);
    }
}
