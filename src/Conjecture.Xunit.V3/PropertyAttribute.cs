using Xunit;

namespace Conjecture.Xunit.V3;

/// <summary>Marks a method as a Conjecture property-based test (xUnit v3).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PropertyAttribute : FactAttribute
{
    /// <summary>Maximum number of examples to generate. Defaults to 100.</summary>
    public int MaxExamples { get; set; } = 100;

    /// <summary>Optional fixed seed for deterministic runs. <see langword="null"/> means use a random seed.</summary>
    public int? Seed { get; set; }

    /// <summary>Whether to use the example database. Defaults to <see langword="true"/>.</summary>
    public bool UseDatabase { get; set; } = true;

    /// <summary>Maximum number of times a strategy may reject a value. Defaults to 5.</summary>
    public int MaxStrategyRejections { get; set; } = 5;

    /// <summary>Deadline for each test run in milliseconds. <see langword="null"/> means no deadline.</summary>
    public int? DeadlineMs { get; set; }
}
