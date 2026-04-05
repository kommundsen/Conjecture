// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Core.Tests;

public class ConjectureSettingsTests
{
    [Fact]
    public void Default_MaxExamples_Is100()
    {
        var settings = new ConjectureSettings();
        Assert.Equal(100, settings.MaxExamples);
    }

    [Fact]
    public void Default_Seed_IsNull()
    {
        var settings = new ConjectureSettings();
        Assert.Null(settings.Seed);
    }

    [Fact]
    public void Constructor_CustomMaxExamples_IsStored()
    {
        var settings = new ConjectureSettings { MaxExamples = 500 };
        Assert.Equal(500, settings.MaxExamples);
    }

    [Fact]
    public void Constructor_CustomSeed_IsStored()
    {
        var settings = new ConjectureSettings { Seed = 42UL };
        Assert.Equal(42UL, settings.Seed);
    }

    [Fact]
    public void MaxExamples_Zero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConjectureSettings { MaxExamples = 0 });
    }

    [Fact]
    public void MaxExamples_Negative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConjectureSettings { MaxExamples = -1 });
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new ConjectureSettings { MaxExamples = 200, Seed = 7UL };
        var b = new ConjectureSettings { MaxExamples = 200, Seed = 7UL };
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new ConjectureSettings { MaxExamples = 100 };
        var b = new ConjectureSettings { MaxExamples = 200 };
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void With_MaxExamples_ReturnsNewRecordWithUpdatedValue()
    {
        var original = new ConjectureSettings { MaxExamples = 100, Seed = 1UL };
        var updated = original with { MaxExamples = 250 };
        Assert.Equal(250, updated.MaxExamples);
        Assert.Equal(1UL, updated.Seed);
        Assert.Equal(100, original.MaxExamples); // original unchanged
    }
}