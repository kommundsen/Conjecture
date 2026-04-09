// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Core.Tests.Internal;

public class SettingsLoggerTests
{
    [Fact]
    public void DefaultSettings_Logger_IsNullLoggerInstance()
    {
        ConjectureSettings settings = new();
        Assert.Same(NullLogger.Instance, settings.Logger);
    }

    [Fact]
    public void Settings_WithLogger_StoresProvidedLogger()
    {
        ILogger logger = NullLoggerFactory.Instance.CreateLogger("test");
        ConjectureSettings settings = new() { Logger = logger };
        Assert.Same(logger, settings.Logger);
    }

    [Fact]
    public void SettingsAttribute_Apply_PreservesBaselineLogger()
    {
        ILogger baseline = NullLoggerFactory.Instance.CreateLogger("baseline");
        ConjectureSettings baselineSettings = new() { Logger = baseline };
        ConjectureSettingsAttribute attr = new();
        ConjectureSettings result = attr.Apply(baselineSettings);
        Assert.Same(baseline, result.Logger);
    }
}