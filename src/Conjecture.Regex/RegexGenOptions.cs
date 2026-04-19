// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Regex;

/// <summary>Options controlling how <c>Generate.Matching</c> generates strings.</summary>
public sealed class RegexGenOptions
{
    /// <summary>Gets whether to sample from ASCII or the full BMP for Unicode categories.</summary>
    public UnicodeCoverage UnicodeCategories { get; init; } = UnicodeCoverage.Ascii;
}