// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Regex;

internal abstract record RegexNode;

internal record Literal(char Ch) : RegexNode;

internal record CharClass(IReadOnlyList<CharRange> Ranges, bool Negated) : RegexNode;

internal record Quantifier(RegexNode Inner, int Min, int? Max, bool Lazy) : RegexNode;

internal record Alternation(IReadOnlyList<RegexNode> Arms) : RegexNode;

internal record Sequence(IReadOnlyList<RegexNode> Items) : RegexNode;

internal record Group(RegexNode Inner, int? CaptureIndex, string? Name) : RegexNode;

internal record Anchor(AnchorKind Kind) : RegexNode;

internal record Backreference(int Index) : RegexNode;

internal record NamedBackreference(string Name) : RegexNode;

internal record LookaroundAssertion(RegexNode Inner, bool IsAhead, bool IsPositive) : RegexNode;

internal record UnicodeCategory(string Category, bool Negated) : RegexNode;

internal record Dot : RegexNode;

internal readonly record struct CharRange(char Low, char High);

internal enum AnchorKind
{
    Start,
    End,
    WordBoundary,
    NonWordBoundary,
}