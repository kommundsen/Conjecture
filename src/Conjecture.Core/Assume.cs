namespace Conjecture.Core;

/// <summary>Provides assumption helpers for filtering property test inputs.</summary>
public static class Assume
{
    /// <summary>Skips the current example if <paramref name="condition"/> is <see langword="false"/>.</summary>
    public static void That(bool condition)
    {
        if (!condition)
        {
            throw new UnsatisfiedAssumptionException();
        }
    }
}
