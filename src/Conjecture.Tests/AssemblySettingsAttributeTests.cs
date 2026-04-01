// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Tests;

public class AssemblySettingsAttributeTests
{
    [Fact]
    public void ConjectureSettingsAttribute_IsSealed()
    {
        Assert.True(typeof(ConjectureSettingsAttribute).IsSealed);
    }

    [Fact]
    public void ConjectureSettingsAttribute_IsAttribute()
    {
        Assert.True(typeof(ConjectureSettingsAttribute).IsSubclassOf(typeof(Attribute)));
    }

    [Fact]
    public void ConjectureSettingsAttribute_HasAssemblyAttributeUsage()
    {
        var usage = typeof(ConjectureSettingsAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Assembly));
    }

    [Fact]
    public void Default_MaxExamples_Is100()
    {
        var attr = new ConjectureSettingsAttribute();
        Assert.Equal(100, attr.MaxExamples);
    }

    [Fact]
    public void Default_UseDatabase_IsTrue()
    {
        var attr = new ConjectureSettingsAttribute();
        Assert.True(attr.UseDatabase);
    }

    [Fact]
    public void Default_MaxStrategyRejections_IsFive()
    {
        var attr = new ConjectureSettingsAttribute();
        Assert.Equal(5, attr.MaxStrategyRejections);
    }

    [Fact]
    public void Default_MaxUnsatisfiedRatio_Is200()
    {
        var attr = new ConjectureSettingsAttribute();
        Assert.Equal(200, attr.MaxUnsatisfiedRatio);
    }

    [Fact]
    public void Default_DatabasePath_IsConjectureExamples()
    {
        var attr = new ConjectureSettingsAttribute();
        Assert.Equal(".conjecture/examples/", attr.DatabasePath);
    }

    [Fact]
    public void MaxExamples_SetTo500_IsStored()
    {
        var attr = new ConjectureSettingsAttribute { MaxExamples = 500 };
        Assert.Equal(500, attr.MaxExamples);
    }

    [Fact]
    public void Apply_MaxExamplesExplicitlySet_OverridesBaseline()
    {
        var attr = new ConjectureSettingsAttribute { MaxExamples = 500 };
        var baseline = new ConjectureSettings { MaxExamples = 100 };

        var result = attr.Apply(baseline);

        Assert.Equal(500, result.MaxExamples);
    }

    [Fact]
    public void Apply_MaxExamplesNotSet_UsesBaseline()
    {
        var attr = new ConjectureSettingsAttribute();
        var baseline = new ConjectureSettings { MaxExamples = 300 };

        var result = attr.Apply(baseline);

        Assert.Equal(300, result.MaxExamples);
    }

    [Fact]
    public void Apply_UseDatabaseExplicitlyFalse_OverridesBaselineTrue()
    {
        var attr = new ConjectureSettingsAttribute { UseDatabase = false };
        var baseline = new ConjectureSettings { UseDatabase = true };

        var result = attr.Apply(baseline);

        Assert.False(result.UseDatabase);
    }

    [Fact]
    public void Apply_AssemblyOverridesJson_MaxExamplesWins()
    {
        // Simulates: JSON loaded settings with MaxExamples=200, assembly attr sets 500
        var attr = new ConjectureSettingsAttribute { MaxExamples = 500 };
        var jsonSettings = new ConjectureSettings { MaxExamples = 200 };

        var result = attr.Apply(jsonSettings);

        Assert.Equal(500, result.MaxExamples);
    }

    [Fact]
    public void Apply_TestLevelOverridesAssembly_WithExpressionWins()
    {
        // Simulates: assembly attr MaxExamples=500, test-level override to 42
        var attr = new ConjectureSettingsAttribute { MaxExamples = 500 };
        var baseline = new ConjectureSettings();

        var assemblyLevel = attr.Apply(baseline);
        var testLevel = assemblyLevel with { MaxExamples = 42 };

        Assert.Equal(42, testLevel.MaxExamples);
    }
}