// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Runtime.CompilerServices;

using Conjecture.Core;

using Xunit;
using Xunit.v3;

using Conjecture.Abstractions.Testing;

namespace Conjecture.Xunit.V3;

/// <summary>Marks a method as a Conjecture property-based test (xUnit v3).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
[XunitTestCaseDiscovererAttribute(typeof(Internal.PropertyTestCaseDiscoverer))]
public sealed class PropertyAttribute(
    [CallerFilePath] string? sourceFilePath = null,
    [CallerLineNumber] int sourceLineNumber = -1) : FactAttribute(sourceFilePath, sourceLineNumber), IPropertyTest
{
    /// <summary>Maximum number of examples to generate. Defaults to 100.</summary>
    public int MaxExamples { get; set; } = 100;

    /// <summary>Optional fixed seed for deterministic runs. 0 means use a random seed.</summary>
    public ulong Seed { get; set; }

    /// <summary>Whether to use the example database. Defaults to <see langword="true"/>.</summary>
    public bool Database { get; set; } = true;

    /// <summary>Maximum number of times a strategy may reject a value. Defaults to 5.</summary>
    public int MaxStrategyRejections { get; set; } = 5;

    /// <summary>Deadline for each test run in milliseconds. 0 means no deadline.</summary>
    public int DeadlineMs { get; set; }

    /// <summary>Whether to run a targeting phase after generation. Defaults to <see langword="true"/>.</summary>
    public bool Targeting { get; set; } = true;

    /// <summary>Fraction of MaxExamples budget allocated to the targeting phase. Defaults to 0.5.</summary>
    public double TargetingProportion { get; set; } = 0.5;
}