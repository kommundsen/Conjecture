// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Constrains generated strings to match <paramref name="pattern"/>.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
public sealed class GenRegexAttribute(string pattern) : Attribute
{
    /// <summary>Gets the regex pattern generated strings must match.</summary>
    public string Pattern { get; } = pattern;
}

