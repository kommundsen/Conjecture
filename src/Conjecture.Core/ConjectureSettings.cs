namespace Conjecture.Core;

/// <summary>Immutable configuration settings for a Conjecture property test.</summary>
public record ConjectureSettings
{
    /// <summary>Maximum number of examples to generate. Must be greater than 0. Defaults to 100.</summary>
    public int MaxExamples
    {
        get => field;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxExamples), value, "Must be greater than 0.");
            }

            field = value;
        }
    } = 100;

    /// <summary>Optional fixed seed for the PRNG. When <see langword="null"/> a random seed is used.</summary>
    public ulong? Seed { get; init; }
}
