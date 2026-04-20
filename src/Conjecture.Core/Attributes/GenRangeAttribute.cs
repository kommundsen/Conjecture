// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Constrains the generated range of a numeric member to [<paramref name="min"/>, <paramref name="max"/>].</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
public sealed class GenRangeAttribute(double min, double max) : Attribute
{
    /// <summary>Gets the inclusive lower bound.</summary>
    public double Min { get; } = min;

    /// <summary>Gets the inclusive upper bound.</summary>
    public double Max { get; } = max;
}

