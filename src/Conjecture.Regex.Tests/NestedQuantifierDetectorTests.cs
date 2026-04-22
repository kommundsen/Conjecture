// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.RegularExpressions;

namespace Conjecture.Regex.Tests;

public sealed class NestedQuantifierDetectorTests
{
    // ── Returns true (nested quantifiers present) ─────────────────────────────

    [Theory]
    [InlineData(@"(a+)+$")]
    [InlineData(@"([a-zA-Z]+)*$")]
    [InlineData(@"(a|aa)+$")]
    [InlineData(@"(a|b)+")] // conservative: any alternation under quantifier is flagged
    public void HasNestedQuantifiers_PatternWithNestedQuantifiers_ReturnsTrue(string pattern)
    {
        RegexNode root = RegexParser.Parse(pattern, RegexOptions.None);

        bool result = NestedQuantifierDetector.HasNestedQuantifiers(root);

        Assert.True(result);
    }

    // ── Returns false (no nested quantifiers) ────────────────────────────────

    [Theory]
    [InlineData(@"a+b+")]
    [InlineData(@"(abc)+")]
    [InlineData(@"[a-z]{3,10}")]
    [InlineData(@"a+|b+")] // sibling quantifiers inside alternation arms, no outer quantifier
    public void HasNestedQuantifiers_PatternWithoutNestedQuantifiers_ReturnsFalse(string pattern)
    {
        RegexNode root = RegexParser.Parse(pattern, RegexOptions.None);

        bool result = NestedQuantifierDetector.HasNestedQuantifiers(root);

        Assert.False(result);
    }
}