// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Xunit.V3;

using Xunit;

namespace Conjecture.Xunit.V3.Tests;

public class XunitV3PropertyAttributeTargetingTests
{
    [Fact]
    public void Targeting_Default_IsTrue()
    {
        PropertyAttribute attr = new();

        Assert.True(attr.Targeting);
    }

    [Fact]
    public void Targeting_CanBeSetToFalse()
    {
        PropertyAttribute attr = new() { Targeting = false };

        Assert.False(attr.Targeting);
    }

    [Fact]
    public void TargetingProportion_Default_IsPointFive()
    {
        PropertyAttribute attr = new();

        Assert.Equal(0.5, attr.TargetingProportion);
    }

    [Fact]
    public void TargetingProportion_CanBeSet()
    {
        PropertyAttribute attr = new() { TargetingProportion = 0.25 };

        Assert.Equal(0.25, attr.TargetingProportion);
    }
}