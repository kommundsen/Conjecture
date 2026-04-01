using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class ConjectureDataReplayTests
{
    [Fact]
    public void ForRecord_ReplayProducesSameInteger()
    {
        var gen = ConjectureData.ForGeneration(new SplittableRandom(1UL));
        var original = gen.NextInteger(0UL, 99UL);

        var replay = ConjectureData.ForRecord(gen.IRNodes);
        var replayed = replay.NextInteger(0UL, 99UL);

        Assert.Equal(original, replayed);
    }

    [Fact]
    public void ForRecord_ReplayProducesSameBoolean()
    {
        var gen = ConjectureData.ForGeneration(new SplittableRandom(2UL));
        var original = gen.NextBoolean();

        var replay = ConjectureData.ForRecord(gen.IRNodes);
        var replayed = replay.NextBoolean();

        Assert.Equal(original, replayed);
    }

    [Fact]
    public void ForRecord_ReplayProducesSameBytes()
    {
        var gen = ConjectureData.ForGeneration(new SplittableRandom(3UL));
        var original = gen.NextBytes(8);

        var replay = ConjectureData.ForRecord(gen.IRNodes);
        var replayed = replay.NextBytes(8);

        Assert.Equal(original, replayed);
    }

    [Fact]
    public void ForRecord_Overrun_SetsStatusAndThrows()
    {
        var gen = ConjectureData.ForGeneration(new SplittableRandom(4UL));
        gen.NextInteger(0UL, 9UL);

        var replay = ConjectureData.ForRecord(gen.IRNodes);
        replay.NextInteger(0UL, 9UL); // consumes the one node

        Assert.Throws<InvalidOperationException>((Action)(() => replay.NextInteger(0UL, 9UL)));
        Assert.Equal(Status.Overrun, replay.Status);
    }
}
