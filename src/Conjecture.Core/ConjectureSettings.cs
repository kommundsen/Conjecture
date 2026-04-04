// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

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

    /// <summary>Whether to use the example database. Defaults to <see langword="true"/>.</summary>
    public bool UseDatabase { get; init; } = true;

    /// <summary>Optional deadline for each test run. When <see langword="null"/> no deadline is enforced.</summary>
    public TimeSpan? Deadline { get; init; }

    /// <summary>Maximum number of times a strategy may reject a value. Must be non-negative. Defaults to 5.</summary>
    public int MaxStrategyRejections
    {
        get => field;
        init => field = ValidateNonNegative(value, nameof(MaxStrategyRejections));
    } = 5;

    /// <summary>Maximum ratio of unsatisfied assumptions. Must be non-negative. Defaults to 200.</summary>
    public int MaxUnsatisfiedRatio
    {
        get => field;
        init => field = ValidateNonNegative(value, nameof(MaxUnsatisfiedRatio));
    } = 200;

    /// <summary>Whether to run a targeting phase after generation. Defaults to <see langword="true"/>.</summary>
    public bool Targeting { get; init; } = true;

    /// <summary>Fraction of <see cref="MaxExamples"/> budget allocated to the targeting phase. Defaults to 0.5.</summary>
    public double TargetingProportion { get; init; } = 0.5;

    private static int ValidateNonNegative(int value, string paramName) =>
        value >= 0 ? value : throw new ArgumentOutOfRangeException(paramName, value, "Must be non-negative.");

    /// <summary>Path to the example database directory. Defaults to <c>.conjecture/examples/</c>.</summary>
    public string DatabasePath { get; init; } = ".conjecture/examples/";
}