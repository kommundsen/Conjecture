using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class ConjectureDataReplayTests
{
    [Fact]
    public void ForRecord_ReplayProducesSameInteger()
    {
        var gen = ConjectureData.ForGeneration(new SplittableRandom(1UL));
        var original = gen.DrawInteger(0UL, 99UL);

        var replay = ConjectureData.ForRecord(gen.IRNodes);
        var replayed = replay.DrawInteger(0UL, 99UL);

        Assert.Equal(original, replayed);
    }

    [Fact]
    public void ForRecord_ReplayProducesSameBoolean()
    {
        var gen = ConjectureData.ForGeneration(new SplittableRandom(2UL));
        var original = gen.DrawBoolean();

        var replay = ConjectureData.ForRecord(gen.IRNodes);
        var replayed = replay.DrawBoolean();

        Assert.Equal(original, replayed);
    }

    [Fact]
    public void ForRecord_ReplayProducesSameBytes()
    {
        var gen = ConjectureData.ForGeneration(new SplittableRandom(3UL));
        var original = gen.DrawBytes(8);

        var replay = ConjectureData.ForRecord(gen.IRNodes);
        var replayed = replay.DrawBytes(8);

        Assert.Equal(original, replayed);
    }

    [Fact]
    public void ForRecord_Overrun_SetsStatusAndThrows()
    {
        var gen = ConjectureData.ForGeneration(new SplittableRandom(4UL));
        gen.DrawInteger(0UL, 9UL);

        var replay = ConjectureData.ForRecord(gen.IRNodes);
        replay.DrawInteger(0UL, 9UL); // consumes the one node

        Assert.Throws<InvalidOperationException>((Action)(() => replay.DrawInteger(0UL, 9UL)));
        Assert.Equal(Status.Overrun, replay.Status);
    }
}
