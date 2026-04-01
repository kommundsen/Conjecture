// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;

namespace Conjecture.Core.Internal;

internal static class SettingsResolver
{
    [RequiresUnreferencedCode("Loads settings from JSON via SettingsLoader, which uses reflection-based deserialization.")]
    internal static ConjectureSettings Resolve(
        string baseDirectory,
        ConjectureSettingsAttribute? assemblyAttribute = null,
        ConjectureSettings? testLevel = null)
    {
        if (testLevel != null)
        {
            return testLevel;
        }
        var fromJson = SettingsLoader.Load(baseDirectory);
        return assemblyAttribute?.Apply(fromJson) ?? fromJson;
    }
}