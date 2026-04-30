// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using NUnit.Framework;
using NUnit.Framework.Interfaces;

using ConjecturePropertyAttribute = Conjecture.NUnit.PropertyAttribute;

namespace Conjecture.NUnit.Tests;

[TestFixture]
public class NUnitPropertyAttributeTests
{
    [Test]
    public void PropertyAttribute_DefaultMaxExamples_Is100()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.That(attr.MaxExamples, Is.EqualTo(100));
    }

    [Test]
    public void PropertyAttribute_DefaultSeed_IsZero()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.That(attr.Seed, Is.EqualTo(0UL));
    }

    [Test]
    public void PropertyAttribute_DefaultDatabase_IsTrue()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.That(attr.Database, Is.True);
    }

    [Test]
    public void PropertyAttribute_DefaultMaxStrategyRejections_Is5()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.That(attr.MaxStrategyRejections, Is.EqualTo(5));
    }

    [Test]
    public void PropertyAttribute_DefaultDeadlineMs_IsZero()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.That(attr.DeadlineMs, Is.EqualTo(0));
    }

    [Test]
    public void PropertyAttribute_MaxExamples_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { MaxExamples = 200 };
        Assert.That(attr.MaxExamples, Is.EqualTo(200));
    }

    [Test]
    public void PropertyAttribute_Seed_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { Seed = 42UL };
        Assert.That(attr.Seed, Is.EqualTo(42UL));
    }

    [Test]
    public void PropertyAttribute_MaxStrategyRejections_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { MaxStrategyRejections = 100 };
        Assert.That(attr.MaxStrategyRejections, Is.EqualTo(100));
    }

    [Test]
    public void PropertyAttribute_DeadlineMs_CanBeSet()
    {
        ConjecturePropertyAttribute attr = new() { DeadlineMs = 5000 };
        Assert.That(attr.DeadlineMs, Is.EqualTo(5000));
    }

    [Test]
    public void PropertyAttribute_AttributeUsage_AllowMultipleFalse()
    {
        AttributeUsageAttribute? usage = typeof(ConjecturePropertyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .OfType<AttributeUsageAttribute>()
            .SingleOrDefault();

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.AllowMultiple, Is.False);
    }

    [Test]
    public void PropertyAttribute_AttributeUsage_TargetsMethod()
    {
        AttributeUsageAttribute? usage = typeof(ConjecturePropertyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .OfType<AttributeUsageAttribute>()
            .SingleOrDefault();

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.ValidOn.HasFlag(AttributeTargets.Method), Is.True);
    }

    [Test]
    public void PropertyAttribute_ImplementsITestBuilder()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.That(attr, Is.InstanceOf<ITestBuilder>());
    }
}