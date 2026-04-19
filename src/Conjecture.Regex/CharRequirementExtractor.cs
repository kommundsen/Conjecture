// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Regex;

/// <summary>
/// Extracts the char ranges that MUST appear somewhere in a string for a given node to match.
/// Used by lookaround handling to post-process generated strings.
/// </summary>
internal static class CharRequirementExtractor
{
    private static readonly CharRange FullRange = new('\u0000', '\uFFFF');

    /// <summary>
    /// Returns the char ranges that must appear in a string for the given node to match.
    /// An empty list means no chars are strictly required (the node can match an empty string,
    /// or we have insufficient static info).
    /// </summary>
    internal static IReadOnlyList<CharRange> ExtractRequired(RegexNode node)
    {
        return node switch
        {
            Literal lit => [new CharRange(lit.Ch, lit.Ch)],

            CharClass { Negated: false } cc => cc.Ranges,

            CharClass { Negated: true } cc => CharRangeHelpers.ComplementRanges(cc.Ranges, '\u0000', '\uFFFF'),

            Dot => [FullRange],

            Quantifier q when q.Min >= 1 => ExtractRequired(q.Inner),

            Quantifier => [],

            Sequence seq => ExtractUnion(seq.Items),

            Alternation alt => ExtractIntersection(alt.Arms),

            Group grp => ExtractRequired(grp.Inner),

            // Anchors, backreferences, lookarounds contribute no char requirements.
            _ => [],
        };
    }

    private static IReadOnlyList<CharRange> ExtractUnion(IReadOnlyList<RegexNode> items)
    {
        List<CharRange> result = [];
        foreach (RegexNode item in items)
        {
            IReadOnlyList<CharRange> reqs = ExtractRequired(item);
            foreach (CharRange r in reqs)
            {
                result.Add(r);
            }
        }

        return result;
    }

    private static IReadOnlyList<CharRange> ExtractIntersection(IReadOnlyList<RegexNode> arms)
    {
        if (arms.Count == 0)
        {
            return [];
        }

        // Start with the requirements of the first arm, then keep only chars present in ALL arms.
        IReadOnlyList<CharRange> common = ExtractRequired(arms[0]);
        for (int i = 1; i < arms.Count; i++)
        {
            IReadOnlyList<CharRange> armReqs = ExtractRequired(arms[i]);
            common = CharRangeHelpers.IntersectRangeLists(common, armReqs);
            if (common.Count == 0)
            {
                return [];
            }
        }

        return common;
    }

    /// <summary>
    /// Returns true if the given ranges together cover the entire BMP ([\s\S] equivalent).
    /// </summary>
    internal static bool CoversFullAlphabet(IReadOnlyList<CharRange> ranges)
    {
        if (ranges.Count == 0)
        {
            return false;
        }

        // Sort and check coverage of [0, 0xFFFF].
        List<(int Lo, int Hi)> sorted = [];
        foreach (CharRange r in ranges)
        {
            sorted.Add((r.Low, r.High));
        }

        sorted.Sort(static (a, b) => a.Lo.CompareTo(b.Lo));

        int cursor = 0;
        foreach ((int lo, int hi) in sorted)
        {
            if (lo > cursor)
            {
                return false;
            }

            int next = hi + 1;
            if (next > cursor)
            {
                cursor = next;
            }
        }

        return cursor > 0xFFFF;
    }
}