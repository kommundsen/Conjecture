// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class ConjectureDataTargetTests
{
    private static ConjectureData Make(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Observations_EmptyByDefault()
    {
        var data = Make();
        Assert.Empty(data.Observations);
    }

    [Fact]
    public void RecordObservation_StoresValue()
    {
        var data = Make();
        data.RecordObservation("label", 42.0);
        Assert.Equal(42.0, data.Observations["label"]);
    }

    [Fact]
    public void RecordObservation_SameLabelOverwrites()
    {
        var data = Make();
        data.RecordObservation("x", 1.0);
        data.RecordObservation("x", 2.0);
        Assert.Equal(2.0, data.Observations["x"]);
    }

    [Fact]
    public void RecordObservation_OnFrozenData_Throws()
    {
        var data = Make();
        data.Freeze();
        Assert.Throws<InvalidOperationException>(() => data.RecordObservation("label", 1.0));
    }

    [Fact]
    public void RecordObservation_NaN_Throws()
    {
        var data = Make();
        Assert.Throws<ArgumentException>(() => data.RecordObservation("label", double.NaN));
    }

    [Fact]
    public void RecordObservation_PositiveInfinity_Throws()
    {
        var data = Make();
        Assert.Throws<ArgumentException>(() => data.RecordObservation("label", double.PositiveInfinity));
    }

    [Fact]
    public void RecordObservation_NegativeInfinity_Throws()
    {
        var data = Make();
        Assert.Throws<ArgumentException>(() => data.RecordObservation("label", double.NegativeInfinity));
    }

    [Fact]
    public void RecordObservation_MultipleLabelsIndependent()
    {
        var data = Make();
        data.RecordObservation("a", 1.0);
        data.RecordObservation("b", 2.0);
        Assert.Equal(1.0, data.Observations["a"]);
        Assert.Equal(2.0, data.Observations["b"]);
    }

    [Fact]
    public void Observations_ReadableAfterFreeze()
    {
        var data = Make();
        data.RecordObservation("label", 99.0);
        data.Freeze();
        Assert.Equal(99.0, data.Observations["label"]);
    }
}
