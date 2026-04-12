// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

    /// <summary>Fraction of <see cref="MaxExamples"/> budget allocated to the targeting phase. Must be in [0.0, 1.0). Defaults to 0.5.</summary>
    public double TargetingProportion
    {
        get => field;
        init
        {
            if (double.IsNaN(value) || value < 0.0 || value >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(TargetingProportion), value, "Must be in [0.0, 1.0).");
            }

            field = value;
        }
    } = 0.5;

    /// <summary>
    /// Creates a <see cref="ConjectureSettings"/> from an <see cref="IPropertyTest"/> attribute.
    /// If <paramref name="attr"/> also implements <see cref="IReproductionExport"/>, those
    /// settings are included; otherwise they default to <see langword="false"/> and the default path.
    /// </summary>
    public static ConjectureSettings From(IPropertyTest attr, ILogger? logger = null)
    {
        return new ConjectureSettings
        {
            MaxExamples = attr.MaxExamples,
            Seed = attr.Seed != 0UL ? attr.Seed : null,
            UseDatabase = attr.UseDatabase,
            MaxStrategyRejections = attr.MaxStrategyRejections,
            Deadline = attr.DeadlineMs > 0 ? TimeSpan.FromMilliseconds(attr.DeadlineMs) : null,
            Targeting = attr.Targeting,
            TargetingProportion = attr.TargetingProportion,
            Logger = logger ?? NullLogger.Instance,
            ExportReproOnFailure = (attr as IReproductionExport)?.ExportReproOnFailure ?? false,
            ReproOutputPath = (attr as IReproductionExport)?.ReproOutputPath ?? ".conjecture/repros/",
        };
    }

    private static int ValidateNonNegative(int value, string paramName) =>
        value >= 0 ? value : throw new ArgumentOutOfRangeException(paramName, value, "Must be non-negative.");

    /// <summary>Path to the example database directory. Defaults to <c>.conjecture/examples/</c>.</summary>
    public string DatabasePath { get; init; } = ".conjecture/examples/";

    /// <summary>Logger for structured test-run output. Defaults to <see cref="NullLogger.Instance"/>.</summary>
    public ILogger Logger { get; init; } = NullLogger.Instance;

    /// <summary>Whether to export a reproduction file on test failure. Defaults to <see langword="false"/>.</summary>
    public bool ExportReproOnFailure { get; init; } = false;

    /// <summary>Output path for exported reproduction files. Defaults to <c>.conjecture/repros/</c>.</summary>
    public string ReproOutputPath { get; init; } = ".conjecture/repros/";
}