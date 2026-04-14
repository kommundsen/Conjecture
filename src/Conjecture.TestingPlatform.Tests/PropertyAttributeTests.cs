// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.TestingPlatform;

namespace Conjecture.TestingPlatform.Tests;

public class PropertyAttributeTests
{
    [Fact]
    public void PropertyAttribute_DefaultValues_MatchConjectureSettingsDefaults()
    {
        PropertyAttribute attribute = new();

        Assert.Equal(100, attribute.MaxExamples);
        Assert.Null(attribute.Seed);
        Assert.True(attribute.UseDatabase);
        Assert.Equal(5, attribute.MaxStrategyRejections);
        Assert.Equal(0, attribute.DeadlineMs);
        Assert.True(attribute.Targeting);
        Assert.Equal(0.5, attribute.TargetingProportion);
        Assert.False(attribute.ExportReproOnFailure);
        Assert.Equal(".conjecture/repros/", attribute.ReproOutputPath);
    }

    [Fact]
    public void PropertyAttribute_SetNonDefaultValues_PropertiesRetainAssignedValues()
    {
        PropertyAttribute attribute = new()
        {
            MaxExamples = 500,
            Seed = 42UL,
            UseDatabase = false,
            MaxStrategyRejections = 10,
            DeadlineMs = 5000,
            Targeting = false,
            TargetingProportion = 0.25,
            ExportReproOnFailure = true,
            ReproOutputPath = "/custom/path/",
        };

        Assert.Equal(500, attribute.MaxExamples);
        Assert.Equal(42UL, attribute.Seed);
        Assert.False(attribute.UseDatabase);
        Assert.Equal(10, attribute.MaxStrategyRejections);
        Assert.Equal(5000, attribute.DeadlineMs);
        Assert.False(attribute.Targeting);
        Assert.Equal(0.25, attribute.TargetingProportion);
        Assert.True(attribute.ExportReproOnFailure);
        Assert.Equal("/custom/path/", attribute.ReproOutputPath);
    }

    [Fact]
    public void PropertyAttribute_AttributeUsage_AllowsExactlyOnePerMethod()
    {
        Type attributeType = typeof(PropertyAttribute);
        AttributeUsageAttribute usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            attributeType,
            typeof(AttributeUsageAttribute))!;

        Assert.NotNull(usage);
        Assert.False(usage.AllowMultiple);
        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
    }
}