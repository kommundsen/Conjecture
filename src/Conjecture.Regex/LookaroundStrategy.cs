// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text;

using Conjecture.Core;

namespace Conjecture.Regex;

/// <summary>
/// Handles <see cref="LookaroundAssertion"/> nodes during string generation.
/// </summary>
internal static class LookaroundStrategy
{
    /// <summary>
    /// Pre-scans a sequence's items for lookaround assertions and returns two collections:
    /// <list type="bullet">
    ///   <item>positiveRequirements — char ranges that MUST appear in the generated body.</item>
    ///   <item>forbiddenFirstChar — char ranges that must NOT be the first character of the body
    ///     (derived from negative lookaheads at the head of the sequence).</item>
    ///   <item>skipBody — set to true when a negative lookahead forbids the entire alphabet,
    ///     meaning the body should be generated at minimum length (ideally empty).</item>
    /// </list>
    /// </summary>
    internal static void AnalyzeSequence(
        IReadOnlyList<RegexNode> items,
        out List<IReadOnlyList<CharRange>> positiveRequirements,
        out List<IReadOnlyList<CharRange>> negativeFirstCharForbidden,
        out bool skipBody)
    {
        positiveRequirements = [];
        negativeFirstCharForbidden = [];
        skipBody = false;

        foreach (RegexNode item in items)
        {
            if (item is not LookaroundAssertion la)
            {
                continue;
            }

            IReadOnlyList<CharRange> innerReqs = CharRequirementExtractor.ExtractRequired(la.Inner);

            if (la.IsAhead && la.IsPositive)
            {
                // (?=...) — the body must contain chars from innerReqs somewhere.
                if (innerReqs.Count > 0)
                {
                    positiveRequirements.Add(innerReqs);
                }
            }
            else if (la.IsAhead && !la.IsPositive)
            {
                // (?!...) — check if it forbids everything.
                if (CharRequirementExtractor.CoversFullAlphabet(innerReqs))
                {
                    // (?![\s\S]) — impossible to have any character; force empty body.
                    skipBody = true;
                }
                else if (innerReqs.Count > 0)
                {
                    negativeFirstCharForbidden.Add(innerReqs);
                }
            }
            // Lookbehind assertions (IsAhead=false) are handled at emit time by the caller.
        }
    }

    /// <summary>
    /// Ensures that for each required char group, at least one char from the group
    /// appears in the body. If not, splices one in at a random position.
    /// </summary>
    internal static void SatisfyPositiveRequirements(
        IGeneratorContext ctx,
        StringBuilder body,
        List<IReadOnlyList<CharRange>> requirements,
        bool firstCharConstrained)
    {
        foreach (IReadOnlyList<CharRange> group in requirements)
        {
            if (group.Count == 0)
            {
                continue;
            }

            bool satisfied = false;
            for (int i = 0; i < body.Length; i++)
            {
                if (CharRangeHelpers.CharInRanges(body[i], group))
                {
                    satisfied = true;
                    break;
                }
            }

            if (!satisfied)
            {
                char needed = CharRangeHelpers.SampleFromRanges(group, ctx);
                if (body.Length == 0)
                {
                    body.Append(needed);
                }
                else
                {
                    // When the first char is constrained by a forbidden-first rule, insertion at
                    // position 0 would overwrite that constraint; clamp the lower bound to 1.
                    int minPos = firstCharConstrained ? 1 : 0;
                    int maxPos = body.Length;
                    int pos = minPos >= maxPos
                        ? maxPos
                        : ctx.Generate(Conjecture.Core.Generate.Integers<int>(minPos, maxPos));
                    body.Insert(pos, needed);
                }
            }
        }
    }

    /// <summary>
    /// Returns the complement of the given forbidden ranges within [0x00, 0xFFFF],
    /// intersected with the allowed ranges.
    /// </summary>
    internal static IReadOnlyList<CharRange> ExcludeForbidden(
        IReadOnlyList<CharRange> allowed,
        IReadOnlyList<CharRange> forbidden)
    {
        if (forbidden.Count == 0)
        {
            return allowed;
        }

        // Build a complement of forbidden, then intersect with allowed.
        IReadOnlyList<CharRange> complement = CharRangeHelpers.ComplementRanges(forbidden, '\u0000', '\uFFFF');
        return CharRangeHelpers.IntersectRangeLists(allowed, complement);
    }
}