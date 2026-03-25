namespace Conjecture.Core;

/// <summary>Thrown when a <c>Where</c> filter exhausts its retry budget, marking the test case as invalid.</summary>
public sealed class UnsatisfiedAssumptionException : Exception
{
    /// <summary>Initializes a new instance of <see cref="UnsatisfiedAssumptionException"/>.</summary>
    public UnsatisfiedAssumptionException()
        : base("Filter budget exhausted — too many values rejected by Where().") { }
}
