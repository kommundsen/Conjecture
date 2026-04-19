// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Regex;

/// <summary>Controls the Unicode character range used when generating Unicode category escapes.</summary>
public enum UnicodeCoverage
{
    /// <summary>Sample only ASCII characters matching the category.</summary>
    Ascii = 0,

    /// <summary>Sample from the full BMP (U+0000 to U+FFFF).</summary>
    Full = 1,
}