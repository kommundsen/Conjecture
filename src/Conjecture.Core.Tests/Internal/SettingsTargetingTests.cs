// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Core.Tests.Internal;

public class SettingsTargetingTests
{
    [Fact]
    public void Targeting_Default_IsTrue()
    {
        ConjectureSettings settings = new();

        Assert.True(settings.Targeting);
    }

    [Fact]
    public void TargetingProportion_Default_IsPointFive()
    {
        ConjectureSettings settings = new();

        Assert.Equal(0.5, settings.TargetingProportion);
    }

    [Fact]
    public void TargetingProportion_Zero_IsValid()
    {
        ConjectureSettings settings = new() { TargetingProportion = 0.0 };

        Assert.Equal(0.0, settings.TargetingProportion);
    }

    [Fact]
    public void TargetingProportion_One_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ConjectureSettings { TargetingProportion = 1.0 });
    }

    [Fact]
    public void TargetingProportion_Negative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ConjectureSettings { TargetingProportion = -0.1 });
    }
}
