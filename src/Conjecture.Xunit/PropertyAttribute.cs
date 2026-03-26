using Xunit;
using Xunit.Sdk;

namespace Conjecture.Xunit;

/// <summary>Marks a method as a Conjecture property-based test.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
[XunitTestCaseDiscoverer("Conjecture.Xunit.Internal.PropertyTestCaseDiscoverer", "Conjecture.Xunit")]
public sealed class PropertyAttribute : FactAttribute
{
    /// <summary>Maximum number of examples to generate. Defaults to 100.</summary>
    public int MaxExamples { get; set; } = 100;

    /// <summary>Optional fixed seed for deterministic runs. 0 means use a random seed.</summary>
    public ulong Seed { get; set; }
}
