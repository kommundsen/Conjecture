// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>
/// Implemented by all framework-specific <c>[Property]</c> attributes.
/// Allows tooling to detect property-based test methods and read common
/// test configuration without depending on framework-specific types.
/// </summary>
public interface IPropertyTest
{
    /// <summary>Maximum number of examples to generate.</summary>
    int MaxExamples { get; }

    /// <summary>Optional fixed seed for deterministic runs. 0 means use a random seed.</summary>
    ulong Seed { get; }

    /// <summary>Whether to use the example database.</summary>
    bool UseDatabase { get; }

    /// <summary>Maximum number of times a strategy may reject a value before the test is abandoned.</summary>
    int MaxStrategyRejections { get; }

    /// <summary>Deadline for each test run in milliseconds. 0 means no deadline.</summary>
    int DeadlineMs { get; }

    /// <summary>Whether to run a targeting phase after the main generation phase.</summary>
    bool Targeting { get; }

    /// <summary>Fraction of the <see cref="MaxExamples"/> budget allocated to the targeting phase.</summary>
    double TargetingProportion { get; }
}