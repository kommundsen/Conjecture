// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class SettingsLoaderTests
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
    public void Load_ValidJson_ParsesMaxExamples()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, """{"maxExamples": 42}""");

        var settings = SettingsLoader.Load(dir);

        Assert.Equal(42, settings.MaxExamples);
    }

    [Fact]
    public void Load_ValidJson_ParsesSeed()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, """{"seed": 99}""");

        var settings = SettingsLoader.Load(dir);

        Assert.Equal(99UL, settings.Seed);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var dir = MakeTempDir();

        var settings = SettingsLoader.Load(dir);

        var defaults = new Conjecture.Core.ConjectureSettings();
        Assert.Equal(defaults.MaxExamples, settings.MaxExamples);
        Assert.Equal(defaults.Seed, settings.Seed);
        Assert.Equal(defaults.UseDatabase, settings.UseDatabase);
        Assert.Equal(defaults.Deadline, settings.Deadline);
    }

    [Fact]
    public void Load_MalformedJson_ThrowsDescriptiveError()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, "{ not valid json }");

        var ex = Assert.Throws<JsonException>(() => SettingsLoader.Load(dir));
        Assert.NotNull(ex.Message);
    }

    [Fact]
    public void Load_PartialJson_UnspecifiedFieldsKeepDefaults()
    {
        var dir = MakeTempDir();
        WriteSettings(dir, """{"maxExamples": 200}""");

        var settings = SettingsLoader.Load(dir);
        var defaults = new Conjecture.Core.ConjectureSettings();

        Assert.Equal(200, settings.MaxExamples);
        Assert.Equal(defaults.Seed, settings.Seed);
        Assert.Equal(defaults.UseDatabase, settings.UseDatabase);
        Assert.Equal(defaults.DatabasePath, settings.DatabasePath);
    }
}