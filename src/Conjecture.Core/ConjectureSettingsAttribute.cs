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

    /// <summary>Maximum number of examples to generate.</summary>
    public int MaxExamples
    {
        get => maxExamples ?? 100;
        set => maxExamples = value;
    }

    /// <summary>Whether to use the example database.</summary>
    public bool UseDatabase
    {
        get => useDatabase ?? true;
        set => useDatabase = value;
    }

    /// <summary>Maximum number of times a strategy may reject a value.</summary>
    public int MaxStrategyRejections
    {
        get => maxStrategyRejections ?? 5;
        set => maxStrategyRejections = value;
    }

    /// <summary>Maximum ratio of unsatisfied assumptions.</summary>
    public int MaxUnsatisfiedRatio
    {
        get => maxUnsatisfiedRatio ?? 200;
        set => maxUnsatisfiedRatio = value;
    }

    /// <summary>Path to the example database directory.</summary>
    public string DatabasePath
    {
        get => databasePath ?? ".conjecture/examples/";
        set => databasePath = value;
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
    };
}
