// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class SettingsResolverTests
{
    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteSettings(string baseDir, string json)
    {
        var settingsDir = Path.Combine(baseDir, ".conjecture");
        Directory.CreateDirectory(settingsDir);
        File.WriteAllText(Path.Combine(settingsDir, "settings.json"), json);
    }

    [Fact]
    public void Resolve_NoOverrides_ReturnsDefaults()
    {
        var dir = MakeTempDir();

        var result = SettingsResolver.Resolve(dir);

        Assert.Equal(new ConjectureSettings(), result);
    }

    [Fact]
    public void Resolve_JsonSetsMaxExamples_JsonOverridesDefaults()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, """{"maxExamples": 42}""");

        var result = SettingsResolver.Resolve(dir);

        Assert.Equal(42, result.MaxExamples);
    }

    [Fact]
    public void Resolve_AssemblyAttributeSetsMaxExamples_AttributeOverridesJson()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, """{"maxExamples": 50}""");
        var attribute = new ConjectureSettingsAttribute { MaxExamples = 200 };

        var result = SettingsResolver.Resolve(dir, attribute);

        Assert.Equal(200, result.MaxExamples);
    }

    [Fact]
    public void Resolve_AssemblyAttributeDoesNotSetMaxExamples_JsonValuePreserved()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, """{"maxExamples": 77}""");
        var attribute = new ConjectureSettingsAttribute { UseDatabase = false };

        var result = SettingsResolver.Resolve(dir, attribute);

        Assert.Equal(77, result.MaxExamples);
        Assert.False(result.UseDatabase);
    }

    [Fact]
    public void Resolve_TestLevelSettings_OverridesAssemblyAttribute()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, """{"maxExamples": 50}""");
        var attribute = new ConjectureSettingsAttribute { MaxExamples = 200 };
        var testLevel = new ConjectureSettings { MaxExamples = 999 };

        var result = SettingsResolver.Resolve(dir, attribute, testLevel);

        Assert.Equal(999, result.MaxExamples);
    }

    [Fact]
    public void Resolve_TestLevelSettings_OverridesAllLowerLayers()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, """{"maxExamples": 50, "useDatabase": false}""");
        var attribute = new ConjectureSettingsAttribute { MaxExamples = 200 };
        var testLevel = new ConjectureSettings { MaxExamples = 1, UseDatabase = true };

        var result = SettingsResolver.Resolve(dir, attribute, testLevel);

        Assert.Equal(1, result.MaxExamples);
        Assert.True(result.UseDatabase);
    }

    [Fact]
    public void Resolve_NoJsonNoAttribute_DefaultsPassThrough()
    {
        var dir = MakeTempDir();
        var attribute = new ConjectureSettingsAttribute();

        var result = SettingsResolver.Resolve(dir, attribute);

        var defaults = new ConjectureSettings();
        Assert.Equal(defaults.MaxExamples, result.MaxExamples);
        Assert.Equal(defaults.UseDatabase, result.UseDatabase);
        Assert.Equal(defaults.MaxStrategyRejections, result.MaxStrategyRejections);
    }

    [Fact]
    public void Resolve_JsonSetsSeed_SeedFlowsThroughAllLayers()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, """{"seed": 12345}""");

        var result = SettingsResolver.Resolve(dir);

        Assert.Equal(12345UL, result.Seed);
    }
}