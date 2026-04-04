// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ConjecturePropertyAttribute = Conjecture.MSTest.PropertyAttribute;

namespace Conjecture.MSTest.Tests;

[TestClass]
public class MSTestPropertyAttributeTargetingTests
{
    [TestMethod]
    public void Targeting_Default_IsTrue()
    {
        ConjecturePropertyAttribute attr = new();

        Assert.IsTrue(attr.Targeting);
    }

    [TestMethod]
    public void Targeting_CanBeSetToFalse()
    {
        ConjecturePropertyAttribute attr = new() { Targeting = false };

        Assert.IsFalse(attr.Targeting);
    }

    [TestMethod]
    public void TargetingProportion_Default_IsPointFive()
    {
        ConjecturePropertyAttribute attr = new();

        Assert.AreEqual(0.5, attr.TargetingProportion);
    }

    [TestMethod]
    public void TargetingProportion_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { TargetingProportion = 0.25 };

        Assert.AreEqual(0.25, attr.TargetingProportion);
    }
}
