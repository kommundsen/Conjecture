using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class ConjectureDataTests
{
    private static ConjectureData Make(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void InitialStatus_IsValid()
    {
        var data = Make();
        Assert.Equal(Status.Valid, data.Status);
    }

    [Fact]
    public void NextInteger_ReturnsValueInRange()
    {
        var data = Make();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(data.NextInteger(0UL, 9UL), 0UL, 9UL);
        }
    }

    [Fact]
    public void NextBoolean_ReturnsBothValues()
    {
        var data = Make();
        var seenTrue = false;
        var seenFalse = false;
        for (var i = 0; i < 1000; i++)
        {
            if (data.NextBoolean()) { seenTrue = true; }
            else { seenFalse = true; }
            if (seenTrue && seenFalse) { break; }
        }
        Assert.True(seenTrue, "NextBoolean never returned true");
        Assert.True(seenFalse, "NextBoolean never returned false");
    }

    [Fact]
    public void NextBytes_ReturnsCorrectLength()
    {
        var data = Make();
        var bytes = data.NextBytes(8);
        Assert.Equal(8, bytes.Length);
    }

    [Fact]
    public void MarkInvalid_SetsStatusInvalid()
    {
        var data = Make();
        data.MarkInvalid();
        Assert.Equal(Status.Invalid, data.Status);
    }

    [Fact]
    public void MarkInteresting_SetsStatusInteresting()
    {
        var data = Make();
        data.MarkInteresting();
        Assert.Equal(Status.Interesting, data.Status);
    }

    [Fact]
    public void Freeze_PreventsNextInteger()
    {
        var data = Make();
        data.Freeze();
        Assert.Throws<InvalidOperationException>(() => data.NextInteger(0UL, 9UL));
    }

    [Fact]
    public void IRNodes_RecordsGenerationsInOrder()
    {
        var data = Make();
        data.NextBoolean();
        data.NextInteger(0UL, 100UL);

        Assert.Equal(2, data.IRNodes.Count);
        Assert.Equal(IRNodeKind.Boolean, data.IRNodes[0].Kind);
        Assert.Equal(IRNodeKind.Integer, data.IRNodes[1].Kind);
    }
}
