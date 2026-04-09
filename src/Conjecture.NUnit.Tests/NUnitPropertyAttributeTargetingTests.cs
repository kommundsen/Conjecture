// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using NUnit.Framework;

using ConjecturePropertyAttribute = Conjecture.NUnit.PropertyAttribute;

namespace Conjecture.NUnit.Tests;

[TestFixture]
public class NUnitPropertyAttributeTargetingTests
{
    [Test]
    public void Targeting_Default_IsTrue()
    {
        ConjecturePropertyAttribute attr = new();

        Assert.That(attr.Targeting, Is.True);
    }

    [Test]
    public void Targeting_CanBeSetToFalse()
    {
        ConjecturePropertyAttribute attr = new() { Targeting = false };

        Assert.That(attr.Targeting, Is.False);
    }

    [Test]
    public void TargetingProportion_Default_IsPointFive()
    {
        ConjecturePropertyAttribute attr = new();

        Assert.That(attr.TargetingProportion, Is.EqualTo(0.5));
    }

    [Test]
    public void TargetingProportion_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { TargetingProportion = 0.25 };

        Assert.That(attr.TargetingProportion, Is.EqualTo(0.25));
    }
}