using Conjecture.Xunit.V3;
using Xunit;

namespace Conjecture.Xunit.V3.Tests;

public class XunitV3PropertyAttributeTests
{
    [Fact]
    public void PropertyAttribute_DefaultMaxExamples_Is100()
    {
        PropertyAttribute attr = new();
        Assert.Equal(100, attr.MaxExamples);
    }

    [Fact]
    public void PropertyAttribute_DefaultSeed_IsNull()
    {
        PropertyAttribute attr = new();
        Assert.Null(attr.Seed);
    }

    [Fact]
    public void PropertyAttribute_DefaultUseDatabase_IsTrue()
    {
        PropertyAttribute attr = new();
        Assert.True(attr.UseDatabase);
    }

    [Fact]
    public void PropertyAttribute_DefaultMaxStrategyRejections_Is5()
    {
        PropertyAttribute attr = new();
        Assert.Equal(5, attr.MaxStrategyRejections);
    }

    [Fact]
    public void PropertyAttribute_DefaultDeadlineMs_IsNull()
    {
        PropertyAttribute attr = new();
        Assert.Null(attr.DeadlineMs);
    }

    [Fact]
    public void PropertyAttribute_InheritsFromFactAttribute()
    {
        PropertyAttribute attr = new();
        Assert.IsAssignableFrom<global::Xunit.FactAttribute>(attr);
    }

    [Fact]
    public void PropertyAttribute_AttributeUsage_AllowMultipleFalse()
    {
        AttributeUsageAttribute? usage = typeof(PropertyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .OfType<AttributeUsageAttribute>()
            .SingleOrDefault();

        Assert.NotNull(usage);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void PropertyAttribute_AttributeUsage_TargetsMethod()
    {
        AttributeUsageAttribute? usage = typeof(PropertyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .OfType<AttributeUsageAttribute>()
            .SingleOrDefault();

        Assert.NotNull(usage);
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Method));
    }

    [Fact]
    public void PropertyAttribute_Seed_CanBeSet()
    {
        PropertyAttribute attr = new() { Seed = 42 };
        Assert.Equal(42, attr.Seed);
    }

    [Fact]
    public void PropertyAttribute_MaxExamples_CanBeSet()
    {
        PropertyAttribute attr = new() { MaxExamples = 200 };
        Assert.Equal(200, attr.MaxExamples);
    }

    [Fact]
    public void PropertyAttribute_MaxStrategyRejections_CanBeSet()
    {
        PropertyAttribute attr = new() { MaxStrategyRejections = 100 };
        Assert.Equal(100, attr.MaxStrategyRejections);
    }

    [Fact]
    public void PropertyAttribute_DeadlineMs_CanBeSet()
    {
        PropertyAttribute attr = new() { DeadlineMs = 5000 };
        Assert.Equal(5000, attr.DeadlineMs);
    }
}
