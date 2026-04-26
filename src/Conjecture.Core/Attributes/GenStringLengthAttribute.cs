// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Constrains generated string length to [<paramref name="minLength"/>, <paramref name="maxLength"/>].</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
public sealed class GenStringLengthAttribute(int minLength, int maxLength) : Attribute
{
    /// <summary>Gets the minimum generated string length.</summary>
    public int MinLength { get; } = minLength;

    /// <summary>Gets the maximum generated string length.</summary>
    public int MaxLength { get; } = maxLength;
}