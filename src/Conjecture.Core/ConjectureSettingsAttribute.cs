// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Assembly-level attribute to configure default settings for all property tests in the assembly.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ConjectureSettingsAttribute : Attribute
{
    private int? maxExamples;
    private bool? useDatabase;
    private int? maxStrategyRejections;
    private int? maxUnsatisfiedRatio;
    private string? databasePath;
    private bool? targeting;
    private double? targetingProportion;

    /// <summary>Maximum number of examples to generate.</summary>
    public int MaxExamples
    {
        get => maxExamples ?? 100;
        init => maxExamples = value;
    }

    /// <summary>Whether to use the example database.</summary>
    public bool UseDatabase
    {
        get => useDatabase ?? true;
        init => useDatabase = value;
    }

    /// <summary>Maximum number of times a strategy may reject a value.</summary>
    public int MaxStrategyRejections
    {
        get => maxStrategyRejections ?? 5;
        init => maxStrategyRejections = value;
    }

    /// <summary>Maximum ratio of unsatisfied assumptions.</summary>
    public int MaxUnsatisfiedRatio
    {
        get => maxUnsatisfiedRatio ?? 200;
        init => maxUnsatisfiedRatio = value;
    }

    /// <summary>Path to the example database directory.</summary>
    public string DatabasePath
    {
        get => databasePath ?? ".conjecture/examples/";
        init => databasePath = value;
    }

    /// <summary>Whether to run a targeting phase after generation.</summary>
    public bool Targeting
    {
        get => targeting ?? true;
        init => targeting = value;
    }

    /// <summary>Fraction of MaxExamples budget allocated to the targeting phase.</summary>
    public double TargetingProportion
    {
        get => targetingProportion ?? 0.5;
        init => targetingProportion = value;
    }

    /// <summary>Returns a <see cref="ConjectureSettings"/> with explicitly-set values overriding <paramref name="baseline"/>.</summary>
    public ConjectureSettings Apply(ConjectureSettings baseline) => new()
    {
        MaxExamples = maxExamples ?? baseline.MaxExamples,
        Seed = baseline.Seed,
        UseDatabase = useDatabase ?? baseline.UseDatabase,
        Deadline = baseline.Deadline,
        MaxStrategyRejections = maxStrategyRejections ?? baseline.MaxStrategyRejections,
        MaxUnsatisfiedRatio = maxUnsatisfiedRatio ?? baseline.MaxUnsatisfiedRatio,
        DatabasePath = databasePath ?? baseline.DatabasePath,
        Targeting = targeting ?? baseline.Targeting,
        TargetingProportion = targetingProportion ?? baseline.TargetingProportion,
        Logger = baseline.Logger,
    };
}