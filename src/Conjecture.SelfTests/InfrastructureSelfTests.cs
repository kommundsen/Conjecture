using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit.V3;
using Xunit;

using ShrinkEngine = Conjecture.Core.Internal.Shrinker;

namespace Conjecture.SelfTests;

public class InfrastructureSelfTests
{
    private sealed class RandomBuffer : IStrategyProvider<byte[]>
    {
        public Strategy<byte[]> Create() =>
            Generate.Lists(Generate.Integers<byte>(), minSize: 1, maxSize: 64)
               .Select(list => list.ToArray());
    }

    private sealed class PositiveInt : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Generate.Integers<int>(1, 10_000);
    }

    private sealed class NonNegativeInt : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Generate.Integers<int>(0, 1_000);
    }

    [Property(MaxExamples = 30)]
    public void DatabaseRoundTrip_SaveThenLoad_ReturnsSameBuffer([From<RandomBuffer>] byte[] buffer)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            using ExampleDatabase db = new(Path.Combine(tempDir, "conjecture.db"));
            db.Save("key", buffer);
            IReadOnlyList<byte[]> loaded = db.Load("key");
            Assert.Single(loaded);
            Assert.Equal(buffer, loaded[0]);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
        }
    }

    [Property(MaxExamples = 100)]
    public void SettingsValidation_ValidValues_ConstructSuccessfully(
        [From<PositiveInt>] int maxExamples,
        [From<NonNegativeInt>] int maxStrategyRejections,
        [From<NonNegativeInt>] int maxUnsatisfiedRatio)
    {
        _ = new ConjectureSettings
        {
            MaxExamples = maxExamples,
            MaxStrategyRejections = maxStrategyRejections,
            MaxUnsatisfiedRatio = maxUnsatisfiedRatio,
        };
    }

    // Verifies ShrinkCount in TestRunResult matches actual shrink iterations produced by
    // running Shrinker.ShrinkAsync independently on the same original counterexample.
    [Fact]
    public async Task ReportingAccuracy_ShrinkCount_MatchesActualShrinkIterations()
    {
        ConjectureSettings settings = new() { Seed = 42ul, MaxExamples = 20, UseDatabase = false };

        static void Predicate(ConjectureData data)
        {
            ulong v = data.NextInteger(0, 1000);
            if (v > 10) throw new InvalidOperationException("too big");
        }

        TestRunResult result = await TestRunner.Run(settings, Predicate);
        Assert.False(result.Passed);

        (IReadOnlyList<IRNode> _, int independentCount) = await ShrinkEngine.ShrinkAsync(
            result.OriginalCounterexample!,
            nodes => new ValueTask<Status>(SelfTestHelpers.Replay(nodes, Predicate)));

        Assert.Equal(independentCount, result.ShrinkCount);
    }
}
