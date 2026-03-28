using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class IntegerStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // ... existing tests ...

    [Fact]
    public void Integers_InvalidRange_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Gen.Integers<int>(10, 5));
    }
}
