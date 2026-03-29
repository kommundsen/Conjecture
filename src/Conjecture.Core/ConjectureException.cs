namespace Conjecture.Core;

/// <summary>Thrown when a Conjecture health check fails (e.g., too many unsatisfied assumptions).</summary>
public sealed class ConjectureException(string message) : Exception(message);
