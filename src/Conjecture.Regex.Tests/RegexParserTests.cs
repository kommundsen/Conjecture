// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text.RegularExpressions;

using RegexUnicodeCategory = Conjecture.Regex.UnicodeCategory;

namespace Conjecture.Regex.Tests;

public class RegexParserTests
{
    // ── Literal ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_LiteralString_ReturnsSequenceOfLiterals()
    {
        RegexNode result = RegexParser.Parse("abc", RegexOptions.None);

        Sequence seq = Assert.IsType<Sequence>(result);
        Assert.Equal(3, seq.Items.Count);
        Literal a = Assert.IsType<Literal>(seq.Items[0]);
        Literal b = Assert.IsType<Literal>(seq.Items[1]);
        Literal c = Assert.IsType<Literal>(seq.Items[2]);
        Assert.Equal('a', a.Ch);
        Assert.Equal('b', b.Ch);
        Assert.Equal('c', c.Ch);
    }

    [Fact]
    public void Parse_SingleLiteral_ReturnsLiteralNode()
    {
        RegexNode result = RegexParser.Parse("x", RegexOptions.None);

        Literal lit = Assert.IsType<Literal>(result);
        Assert.Equal('x', lit.Ch);
    }

    // ── CharClass ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("[a-z]", false)]
    [InlineData("[^0-9]", true)]
    public void Parse_CharClass_ReturnsCharClassWithCorrectNegation(string pattern, bool expectedNegated)
    {
        RegexNode result = RegexParser.Parse(pattern, RegexOptions.None);

        CharClass cc = Assert.IsType<CharClass>(result);
        Assert.Equal(expectedNegated, cc.Negated);
        Assert.NotEmpty(cc.Ranges);
    }

    [Fact]
    public void Parse_CharClassAtoZ_HasCorrectRange()
    {
        RegexNode result = RegexParser.Parse("[a-z]", RegexOptions.None);

        CharClass cc = Assert.IsType<CharClass>(result);
        Assert.False(cc.Negated);
        Assert.Equal(1, cc.Ranges.Count);
        Assert.Equal('a', cc.Ranges[0].Low);
        Assert.Equal('z', cc.Ranges[0].High);
    }

    [Fact]
    public void Parse_CharClassNegatedDigits_HasCorrectRangeAndNegated()
    {
        RegexNode result = RegexParser.Parse("[^0-9]", RegexOptions.None);

        CharClass cc = Assert.IsType<CharClass>(result);
        Assert.True(cc.Negated);
        Assert.Equal('0', cc.Ranges[0].Low);
        Assert.Equal('9', cc.Ranges[0].High);
    }

    [Fact]
    public void Parse_CharClassWordShorthand_ReturnsCharClassNode()
    {
        // [\w] is a shorthand char class
        RegexNode result = RegexParser.Parse(@"[\w]", RegexOptions.None);

        Assert.IsType<CharClass>(result);
    }

    // ── Quantifiers ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("a*", 0, null, false)]
    [InlineData("a+", 1, null, false)]
    [InlineData("a?", 0, 1, false)]
    [InlineData("a{3}", 3, 3, false)]
    [InlineData("a{2,5}", 2, 5, false)]
    [InlineData("a*?", 0, null, true)]
    public void Parse_Quantifier_ReturnsCorrectMinMaxLazy(
        string pattern,
        int expectedMin,
        int? expectedMax,
        bool expectedLazy)
    {
        RegexNode result = RegexParser.Parse(pattern, RegexOptions.None);

        Quantifier q = Assert.IsType<Quantifier>(result);
        Assert.Equal(expectedMin, q.Min);
        Assert.Equal(expectedMax, q.Max);
        Assert.Equal(expectedLazy, q.Lazy);
        Assert.IsType<Literal>(q.Inner);
    }

    // ── Alternation ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Alternation_ReturnsTwoArmAlternation()
    {
        RegexNode result = RegexParser.Parse("cat|dog", RegexOptions.None);

        Alternation alt = Assert.IsType<Alternation>(result);
        Assert.Equal(2, alt.Arms.Count);
    }

    // ── Groups ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CapturingGroup_ReturnsCaptureIndexOne()
    {
        RegexNode result = RegexParser.Parse("(foo)", RegexOptions.None);

        Group grp = Assert.IsType<Group>(result);
        Assert.Equal(1, grp.CaptureIndex);
        Assert.Null(grp.Name);
    }

    [Fact]
    public void Parse_NamedGroup_ReturnsGroupWithName()
    {
        RegexNode result = RegexParser.Parse(@"(?<year>\d{4})", RegexOptions.None);

        Group grp = Assert.IsType<Group>(result);
        Assert.Equal("year", grp.Name);
        Assert.NotNull(grp.CaptureIndex);
    }

    // ── Anchors ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("^", 0)]
    [InlineData("$", 1)]
    [InlineData(@"\b", 2)]
    public void Parse_Anchor_ReturnsCorrectAnchorKind(string pattern, int expectedKind)
    {
        RegexNode result = RegexParser.Parse(pattern, RegexOptions.None);

        Anchor anchor = Assert.IsType<Anchor>(result);
        Assert.Equal((AnchorKind)expectedKind, anchor.Kind);
    }

    // ── Dot ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Dot_WithoutSingleline_ReturnsDotNode()
    {
        // Without Singleline, dot matches any char except newline.
        // The AST shape is Dot regardless; Singleline is carried by RegexOptions at
        // evaluation time, not encoded in the node.
        RegexNode result = RegexParser.Parse(".", RegexOptions.None);

        Assert.IsType<Dot>(result);
    }

    [Fact]
    public void Parse_Dot_WithSingleline_ReturnsDotNode()
    {
        // With Singleline, dot matches newlines too. AST shape remains Dot.
        RegexNode result = RegexParser.Parse(".", RegexOptions.Singleline);

        Assert.IsType<Dot>(result);
    }

    // ── Unicode categories ────────────────────────────────────────────────────

    [Fact]
    public void Parse_UnicodeCategoryPositive_ReturnsCategoryNode()
    {
        RegexNode result = RegexParser.Parse(@"\p{L}", RegexOptions.None);

        RegexUnicodeCategory cat = Assert.IsType<RegexUnicodeCategory>(result);
        Assert.Equal("L", cat.Category);
        Assert.False(cat.Negated);
    }

    [Fact]
    public void Parse_UnicodeCategoryNegated_ReturnsCategoryNodeWithNegatedTrue()
    {
        RegexNode result = RegexParser.Parse(@"\P{Nd}", RegexOptions.None);

        RegexUnicodeCategory cat = Assert.IsType<RegexUnicodeCategory>(result);
        Assert.Equal("Nd", cat.Category);
        Assert.True(cat.Negated);
    }

    // ── Lookaround ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PositiveLookahead_ReturnsLookaroundAheadPositive()
    {
        RegexNode result = RegexParser.Parse(@"(?=\d)", RegexOptions.None);

        LookaroundAssertion la = Assert.IsType<LookaroundAssertion>(result);
        Assert.True(la.IsAhead);
        Assert.True(la.IsPositive);
    }

    [Fact]
    public void Parse_NegativeLookahead_ReturnsLookaroundAheadNegative()
    {
        RegexNode result = RegexParser.Parse(@"(?!\d)", RegexOptions.None);

        LookaroundAssertion la = Assert.IsType<LookaroundAssertion>(result);
        Assert.True(la.IsAhead);
        Assert.False(la.IsPositive);
    }

    [Fact]
    public void Parse_PositiveLookbehind_ReturnsLookaroundBehindPositive()
    {
        RegexNode result = RegexParser.Parse(@"(?<=\s)", RegexOptions.None);

        LookaroundAssertion la = Assert.IsType<LookaroundAssertion>(result);
        Assert.False(la.IsAhead);
        Assert.True(la.IsPositive);
    }

    [Fact]
    public void Parse_NegativeLookbehind_ReturnsLookaroundBehindNegative()
    {
        RegexNode result = RegexParser.Parse(@"(?<!\s)", RegexOptions.None);

        LookaroundAssertion la = Assert.IsType<LookaroundAssertion>(result);
        Assert.False(la.IsAhead);
        Assert.False(la.IsPositive);
    }

    // ── Backreferences ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NumericBackreference_ReturnsBackreferenceWithIndex()
    {
        // (a)\1 — group 1 then backreference to group 1
        RegexNode result = RegexParser.Parse(@"(a)\1", RegexOptions.None);

        Sequence seq = Assert.IsType<Sequence>(result);
        Assert.Equal(2, seq.Items.Count);
        Assert.IsType<Group>(seq.Items[0]);
        Backreference bref = Assert.IsType<Backreference>(seq.Items[1]);
        Assert.Equal(1, bref.Index);
    }

    [Fact]
    public void Parse_NamedBackreference_ReturnsNamedBackreferenceWithName()
    {
        // (?<w>\w+)\k<w>
        RegexNode result = RegexParser.Parse(@"(?<w>\w+)\k<w>", RegexOptions.None);

        Sequence seq = Assert.IsType<Sequence>(result);
        Assert.Equal(2, seq.Items.Count);
        Assert.IsType<Group>(seq.Items[0]);
        NamedBackreference nbref = Assert.IsType<NamedBackreference>(seq.Items[1]);
        Assert.Equal("w", nbref.Name);
    }

    // ── CharRange struct ─────────────────────────────────────────────────────

    [Fact]
    public void CharRange_LowAndHighProperties_RoundTrip()
    {
        CharRange range = new('a', 'z');

        Assert.Equal('a', range.Low);
        Assert.Equal('z', range.High);
    }
}