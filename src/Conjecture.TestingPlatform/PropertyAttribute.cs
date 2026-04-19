// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.TestingPlatform;

/// <summary>Marks a method as a Conjecture property-based test.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PropertyAttribute : Attribute, IPropertyTest, IReproductionExport
{
    /// <summary>Maximum number of examples to generate. Defaults to 100.</summary>
    public int MaxExamples { get; set; } = 100;

    /// <summary>Optional fixed seed for deterministic runs. 0 means use a random seed.</summary>
    public ulong Seed { get; set; }

    /// <summary>Whether to use the example database. Defaults to <see langword="true"/>.</summary>
    public bool UseDatabase { get; set; } = true;

    /// <summary>Maximum number of times a strategy may reject a value. Defaults to 5.</summary>
    public int MaxStrategyRejections { get; set; } = 5;

    /// <summary>Deadline for each test run in milliseconds. 0 means no deadline.</summary>
    public int DeadlineMs { get; set; } = 0;

    /// <summary>Whether to run a targeting phase after generation. Defaults to <see langword="true"/>.</summary>
    public bool Targeting { get; set; } = true;

    /// <summary>Fraction of MaxExamples budget allocated to the targeting phase. Defaults to 0.5.</summary>
    public double TargetingProportion { get; set; } = 0.5;

    /// <summary>Whether to export a reproduction file on test failure. Defaults to <see langword="false"/>.</summary>
    public bool ExportReproOnFailure { get; set; } = false;

    /// <summary>Output path for exported reproduction files. Defaults to <c>.conjecture/repros/</c>.</summary>
    public string ReproOutputPath { get; set; } = ".conjecture/repros/";
}