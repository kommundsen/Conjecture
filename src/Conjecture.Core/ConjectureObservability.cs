// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Conjecture.Core;

/// <summary>Static singletons for Conjecture's OpenTelemetry <see cref="ActivitySource"/> and <see cref="Meter"/>.</summary>
public static class ConjectureObservability
{
    internal const string SchemaUrl = "https://github.com/kommundsen/Conjecture/blob/main/docs/telemetry-schema.json";

    private static readonly string Version =
        typeof(ConjectureObservability).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

    /// <summary>Gets the <see cref="System.Diagnostics.ActivitySource"/> used to emit Conjecture trace spans.</summary>
    public static ActivitySource ActivitySource { get; } = new("Conjecture.Core", Version);

    /// <summary>Gets the <see cref="System.Diagnostics.Metrics.Meter"/> used to emit Conjecture metrics.</summary>
    public static Meter Meter { get; } = new("Conjecture.Core", Version);
}