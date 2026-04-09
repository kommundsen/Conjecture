// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ConjecturePropertyAttribute = Conjecture.MSTest.PropertyAttribute;

namespace Conjecture.MSTest.Tests;

[TestClass]
public class MSTestPropertyAttributeTests
{
    [TestMethod]
    public void PropertyAttribute_InheritsTestMethodAttribute()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.IsInstanceOfType<global::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute>(attr);
    }

    [TestMethod]
    public void PropertyAttribute_DefaultMaxExamples_Is100()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.AreEqual(100, attr.MaxExamples);
    }

    [TestMethod]
    public void PropertyAttribute_DefaultSeed_IsZero()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.AreEqual(0UL, attr.Seed);
    }

    [TestMethod]
    public void PropertyAttribute_DefaultUseDatabase_IsTrue()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.IsTrue(attr.UseDatabase);
    }

    [TestMethod]
    public void PropertyAttribute_DefaultMaxStrategyRejections_Is5()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.AreEqual(5, attr.MaxStrategyRejections);
    }

    [TestMethod]
    public void PropertyAttribute_DefaultDeadlineMs_IsZero()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.AreEqual(0, attr.DeadlineMs);
    }

    [TestMethod]
    public void PropertyAttribute_MaxExamples_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { MaxExamples = 500 };
        Assert.AreEqual(500, attr.MaxExamples);
    }

    [TestMethod]
    public void PropertyAttribute_Seed_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { Seed = 0xDEADBEEFUL };
        Assert.AreEqual(0xDEADBEEFUL, attr.Seed);
    }

    [TestMethod]
    public void PropertyAttribute_UseDatabase_CanBeSetFalse()
    {
        ConjecturePropertyAttribute attr = new() { UseDatabase = false };
        Assert.IsFalse(attr.UseDatabase);
    }

    [TestMethod]
    public void PropertyAttribute_MaxStrategyRejections_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { MaxStrategyRejections = 20 };
        Assert.AreEqual(20, attr.MaxStrategyRejections);
    }

    [TestMethod]
    public void PropertyAttribute_DeadlineMs_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { DeadlineMs = 5000 };
        Assert.AreEqual(5000, attr.DeadlineMs);
    }

    [TestMethod]
    public void PropertyAttribute_AttributeUsage_TargetsMethod()
    {
        AttributeUsageAttribute? usage = typeof(ConjecturePropertyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .OfType<AttributeUsageAttribute>()
            .SingleOrDefault();

        Assert.IsNotNull(usage);
        Assert.IsTrue(usage!.ValidOn.HasFlag(AttributeTargets.Method));
    }

    [TestMethod]
    public void PropertyAttribute_AttributeUsage_AllowMultipleFalse()
    {
        AttributeUsageAttribute? usage = typeof(ConjecturePropertyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .OfType<AttributeUsageAttribute>()
            .SingleOrDefault();

        Assert.IsNotNull(usage);
        Assert.IsFalse(usage!.AllowMultiple);
    }
}