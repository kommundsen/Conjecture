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
    public void DrawInteger_ReturnsValueInRange()
    {
        var data = Make();
        for (var i = 0; i < 100; i++)
            Assert.InRange(data.DrawInteger(0UL, 9UL), 0UL, 9UL);
    }

    [Fact]
    public void DrawBoolean_ReturnsBothValues()
    {
        var data = Make();
        var seenTrue = false;
        var seenFalse = false;
        for (var i = 0; i < 1000; i++)
        {
            if (data.DrawBoolean()) seenTrue = true;
            else seenFalse = true;
            if (seenTrue && seenFalse) break;
        }
        Assert.True(seenTrue, "DrawBoolean never returned true");
        Assert.True(seenFalse, "DrawBoolean never returned false");
    }

    [Fact]
    public void DrawBytes_ReturnsCorrectLength()
    {
        var data = Make();
        var bytes = data.DrawBytes(8);
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
    public void Freeze_PreventsDrawInteger()
    {
        var data = Make();
        data.Freeze();
        Assert.Throws<InvalidOperationException>(() => data.DrawInteger(0UL, 9UL));
    }

    [Fact]
    public void IRNodes_RecordsDrawsInOrder()
    {
        var data = Make();
        data.DrawBoolean();
        data.DrawInteger(0UL, 100UL);

        Assert.Equal(2, data.IRNodes.Count);
        Assert.Equal(IRNodeKind.Boolean, data.IRNodes[0].Kind);
        Assert.Equal(IRNodeKind.Integer, data.IRNodes[1].Kind);
    }
}
