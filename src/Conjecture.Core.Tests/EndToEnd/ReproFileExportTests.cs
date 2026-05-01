// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Xunit.V3;

namespace Conjecture.Core.Tests.EndToEnd;

/// <summary>
/// End-to-end tests for the reproduction-export feature using the public API.
/// White-box format tests for <c>ReproFileBuilder.Build()</c> live in
/// <c>Conjecture.Xunit.Tests/Internal/ReproExportIntegrationTests.cs</c>.
/// </summary>
public sealed class ReproFileExportTests
{
    // ── ConjectureSettings public defaults ───────────────────────────────

    [Fact]
    public void ConjectureSettings_ExportReproductionOnFailure_DefaultIsFalse()
    {
        ConjectureSettings settings = new();
        Assert.False(settings.ExportReproductionOnFailure);
    }

    [Fact]
    public void ConjectureSettings_ReproductionOutputPath_DefaultIsConjecturePath()
    {
        ConjectureSettings settings = new();
        Assert.Equal(".conjecture/repros/", settings.ReproductionOutputPath);
    }

    // ── PropertyAttribute (xUnit v3) public defaults ─────────────────────

    [Fact]
    public void PropertyAttribute_ExportReproductionOnFailure_DefaultIsFalse()
    {
        PropertyAttribute attr = new();
        Assert.False(attr.ExportReproductionOnFailure);
    }

    [Fact]
    public void PropertyAttribute_ExportReproductionOnFailure_CanBeSetToTrue()
    {
        PropertyAttribute attr = new() { ExportReproductionOnFailure = true };
        Assert.True(attr.ExportReproductionOnFailure);
    }

    [Fact]
    public void PropertyAttribute_ReproductionOutputPath_DefaultIsConjecturePath()
    {
        PropertyAttribute attr = new();
        Assert.Equal(".conjecture/repros/", attr.ReproductionOutputPath);
    }

    [Fact]
    public void PropertyAttribute_ReproductionOutputPath_CanBeCustomised()
    {
        PropertyAttribute attr = new() { ReproductionOutputPath = "/tmp/my-repros/" };
        Assert.Equal("/tmp/my-repros/", attr.ReproductionOutputPath);
    }

    // ── ConjectureSettings.From carries export settings from attribute ────

    [Fact]
    public void ConjectureSettingsFrom_CarriesExportReproductionOnFailure()
    {
        PropertyAttribute attr = new() { ExportReproductionOnFailure = true };
        ConjectureSettings settings = ConjectureSettings.From(attr);
        Assert.True(settings.ExportReproductionOnFailure);
    }

    [Fact]
    public void ConjectureSettingsFrom_CarriesReproductionOutputPath()
    {
        PropertyAttribute attr = new() { ReproductionOutputPath = "/custom/path/" };
        ConjectureSettings settings = ConjectureSettings.From(attr);
        Assert.Equal("/custom/path/", settings.ReproductionOutputPath);
    }
}
