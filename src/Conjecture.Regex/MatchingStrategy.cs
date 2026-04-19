// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Conjecture.Core;
using Conjecture.Core.Internal;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Regex;

internal sealed class MatchingStrategy(
    RegexNode root,
    DotNetRegex regex,
    RegexOptions regexOptions,
    RegexGenOptions genOptions) : Strategy<string>
{
    // Precomputed BMP candidates per Unicode category, built once at class init.
    private static readonly Dictionary<System.Globalization.UnicodeCategory, char[]> BmpCandidates =
        BuildBmpCandidates();

    private static Dictionary<System.Globalization.UnicodeCategory, char[]> BuildBmpCandidates()
    {
        Dictionary<System.Globalization.UnicodeCategory, List<char>> buckets = [];
        foreach (System.Globalization.UnicodeCategory cat in Enum.GetValues<System.Globalization.UnicodeCategory>())
        {
            buckets[cat] = [];
        }

        for (int cp = 0; cp <= 0xFFFF; cp++)
        {
            char c = (char)cp;
            System.Globalization.UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            buckets[cat].Add(c);
        }

        Dictionary<System.Globalization.UnicodeCategory, char[]> result = [];
        foreach (KeyValuePair<System.Globalization.UnicodeCategory, List<char>> kv in buckets)
        {
            result[kv.Key] = [.. kv.Value];
        }

        return result;
    }

    private readonly bool ignoreCase = (regexOptions & RegexOptions.IgnoreCase) != 0;
    private readonly bool singleline = (regexOptions & RegexOptions.Singleline) != 0;
    private readonly bool hasLookaround = ContainsLookaround(root);

    private static bool ContainsLookaround(RegexNode node)
    {
        return node switch
        {
            LookaroundAssertion => true,
            Sequence seq => ContainsLookaroundInList(seq.Items),
            Alternation alt => ContainsLookaroundInList(alt.Arms),
            Group grp => ContainsLookaround(grp.Inner),
            Quantifier q => ContainsLookaround(q.Inner),
            _ => false,
        };
    }

    private static bool ContainsLookaroundInList(IReadOnlyList<RegexNode> items)
    {
        foreach (RegexNode item in items)
        {
            if (ContainsLookaround(item))
            {
                return true;
            }
        }

        return false;
    }

    internal override string Generate(ConjectureData data)
    {
        return hasLookaround
            ? Conjecture.Core.Generate.Compose<string>(ctx =>
            {
                const int maxAttempts = 50;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    StringBuilder sb = new();
                    Dictionary<int, string> captures = [];
                    Dictionary<string, string> namedCaptures = [];
                    GenerateNode(ctx, root, sb, captures, namedCaptures);
                    string candidate = sb.ToString();
                    if (regex.IsMatch(candidate))
                    {
                        return candidate;
                    }
                }

                data.MarkInvalid();
                throw new UnsatisfiedAssumptionException();
            }).Generate(data)
            : Conjecture.Core.Generate.Compose<string>(ctx =>
        {
            StringBuilder sb = new();
            Dictionary<int, string> captures = [];
            Dictionary<string, string> namedCaptures = [];
            GenerateNode(ctx, root, sb, captures, namedCaptures);
            return sb.ToString();
        }).Generate(data);
    }

    private void GenerateNode(
        IGeneratorContext ctx,
        RegexNode node,
        StringBuilder sb,
        Dictionary<int, string> captures,
        Dictionary<string, string> namedCaptures)
    {
        switch (node)
        {
            case Literal lit:
                GenerateLiteral(ctx, lit.Ch, sb);
                break;

            case CharClass cc:
                GenerateCharClass(ctx, cc, sb);
                break;

            case Quantifier q:
                GenerateQuantifier(ctx, q, sb, captures, namedCaptures);
                break;

            case Alternation alt:
                {
                    int idx = ctx.Generate(Conjecture.Core.Generate.Integers<int>(0, alt.Arms.Count - 1));
                    GenerateNode(ctx, alt.Arms[idx], sb, captures, namedCaptures);
                    break;
                }

            case Sequence seq:
                GenerateSequence(ctx, seq, sb, captures, namedCaptures);
                break;

            case Group grp:
                {
                    int startPos = sb.Length;
                    GenerateNode(ctx, grp.Inner, sb, captures, namedCaptures);
                    string captured = sb.ToString(startPos, sb.Length - startPos);
                    if (grp.CaptureIndex.HasValue)
                    {
                        captures[grp.CaptureIndex.Value] = captured;
                    }

                    if (grp.Name is not null)
                    {
                        namedCaptures[grp.Name] = captured;
                    }

                    break;
                }

            case Anchor:
                // Anchors produce no output; the match validator checks position.
                break;

            case Dot:
                GenerateDot(ctx, sb);
                break;

            case UnicodeCategory uc:
                GenerateUnicodeCategory(ctx, uc, sb);
                break;

            case Backreference br:
                sb.Append(captures.TryGetValue(br.Index, out string? text) ? text : string.Empty);
                break;

            case NamedBackreference nbr:
                sb.Append(namedCaptures.TryGetValue(nbr.Name, out string? namedText) ? namedText : string.Empty);
                break;

            case LookaroundAssertion:
                // Lookarounds are handled at the Sequence level; a lone lookaround produces no output.
                break;

            default:
                throw new NotSupportedException($"Unsupported node type: {node.GetType().Name}");
        }
    }

    private void GenerateSequence(
        IGeneratorContext ctx,
        Sequence seq,
        StringBuilder sb,
        Dictionary<int, string> captures,
        Dictionary<string, string> namedCaptures)
    {
        LookaroundStrategy.AnalyzeSequence(
            seq.Items,
            out List<IReadOnlyList<CharRange>> positiveReqs,
            out List<IReadOnlyList<CharRange>> negativeFirstCharForbidden,
            out bool skipBody);

        // Emit any positive-lookbehind prefixes so IsMatch can find the required preceding char.
        foreach (RegexNode item in seq.Items)
        {
            if (item is LookaroundAssertion { IsAhead: false, IsPositive: true } posLb)
            {
                IReadOnlyList<CharRange> reqs = CharRequirementExtractor.ExtractRequired(posLb.Inner);
                if (reqs.Count > 0)
                {
                    // Prepend a char that satisfies the lookbehind requirement.
                    char prefix = CharRangeHelpers.SampleFromRanges(reqs, ctx);
                    sb.Append(prefix);
                }

                break; // Only one lookbehind prefix needed.
            }
        }

        // Collect negative-lookahead forbidden-first-char constraints.
        List<CharRange> combinedForbidden = [];
        foreach (IReadOnlyList<CharRange> forbidden in negativeFirstCharForbidden)
        {
            foreach (CharRange r in forbidden)
            {
                combinedForbidden.Add(r);
            }
        }

        bool firstCharConstrained = combinedForbidden.Count > 0;
        bool firstBodyChar = firstCharConstrained;

        if (skipBody)
        {
            // (?![\s\S]) — impossible lookahead: produce empty string.
            return;
        }

        // Normal path: emit body nodes, applying first-char constraint where needed.
        int bodyStart = sb.Length;
        foreach (RegexNode item in seq.Items)
        {
            if (item is LookaroundAssertion)
            {
                // Already processed above; skip generation.
                continue;
            }

            if (firstBodyChar && sb.Length == bodyStart)
            {
                // Intercept the first char-producing node to apply the forbidden constraint.
                firstBodyChar = false;
                GenerateNodeWithForbiddenFirst(ctx, item, sb, captures, namedCaptures, combinedForbidden);
            }
            else
            {
                GenerateNode(ctx, item, sb, captures, namedCaptures);
            }
        }

        // Post-process: splice in any missing required chars for positive lookaheads.
        if (positiveReqs.Count > 0)
        {
            LookaroundStrategy.SatisfyPositiveRequirements(ctx, sb, positiveReqs, firstCharConstrained);
        }
    }

    /// <summary>
    /// Generates a node that must not begin with a char in <paramref name="forbidden"/>.
    /// Applies only to the first character-producing leaf of the node.
    /// </summary>
    private void GenerateNodeWithForbiddenFirst(
        IGeneratorContext ctx,
        RegexNode node,
        StringBuilder sb,
        Dictionary<int, string> captures,
        Dictionary<string, string> namedCaptures,
        List<CharRange> forbidden)
    {
        switch (node)
        {
            case CharClass cc:
                GenerateCharClassExcluding(ctx, cc, sb, forbidden);
                break;

            case Quantifier q when q.Min >= 1:
                // The first iteration of the quantifier must obey the constraint.
                GenerateNodeWithForbiddenFirst(ctx, q.Inner, sb, captures, namedCaptures, forbidden);
                // Remaining repetitions are unconstrained.
                int maxCount = q.Max ?? (q.Min + 16);
                int count = ctx.Generate(Conjecture.Core.Generate.Integers<int>(q.Min, maxCount));
                for (int i = 1; i < count; i++)
                {
                    GenerateNode(ctx, q.Inner, sb, captures, namedCaptures);
                }

                break;

            case Group grp:
                {
                    int startPos = sb.Length;
                    GenerateNodeWithForbiddenFirst(ctx, grp.Inner, sb, captures, namedCaptures, forbidden);
                    string captured = sb.ToString(startPos, sb.Length - startPos);
                    if (grp.CaptureIndex.HasValue)
                    {
                        captures[grp.CaptureIndex.Value] = captured;
                    }

                    if (grp.Name is not null)
                    {
                        namedCaptures[grp.Name] = captured;
                    }

                    break;
                }

            case Alternation alt:
                {
                    int idx = ctx.Generate(Conjecture.Core.Generate.Integers<int>(0, alt.Arms.Count - 1));
                    RegexNode chosenArm = alt.Arms[idx];
                    // Recurse into the chosen arm with the forbidden constraint on its first node.
                    if (chosenArm is Sequence armSeq && armSeq.Items.Count > 0)
                    {
                        GenerateNodeWithForbiddenFirst(ctx, armSeq.Items[0], sb, captures, namedCaptures, forbidden);
                        for (int i = 1; i < armSeq.Items.Count; i++)
                        {
                            GenerateNode(ctx, armSeq.Items[i], sb, captures, namedCaptures);
                        }
                    }
                    else
                    {
                        GenerateNodeWithForbiddenFirst(ctx, chosenArm, sb, captures, namedCaptures, forbidden);
                    }

                    break;
                }

            case Literal lit:
                // If literal char is in the forbidden set, emit it anyway (unresolvable contradiction).
                GenerateLiteral(ctx, lit.Ch, sb);
                break;

            default:
                // Fall back to unconstrained generation for other node types.
                GenerateNode(ctx, node, sb, captures, namedCaptures);
                break;
        }
    }

    private void GenerateCharClassExcluding(
        IGeneratorContext ctx,
        CharClass cc,
        StringBuilder sb,
        List<CharRange> forbidden)
    {
        IReadOnlyList<CharRange> allowed = cc.Negated
            ? CharRangeHelpers.ComplementRanges(cc.Ranges, '\u0000', '\uFFFF')
            : (IReadOnlyList<CharRange>)cc.Ranges;

        if (ignoreCase)
        {
            allowed = ExpandCaseInsensitive(allowed);
        }

        IReadOnlyList<CharRange> filtered = LookaroundStrategy.ExcludeForbidden(allowed, forbidden);
        if (filtered.Count == 0)
        {
            // No valid chars after exclusion — fall back to unconstrained.
            filtered = allowed;
        }

        char ch = CharRangeHelpers.SampleFromRanges(filtered, ctx);
        sb.Append(ch);
    }

    private void GenerateLiteral(IGeneratorContext ctx, char ch, StringBuilder sb)
    {
        if (ignoreCase && char.IsLetter(ch))
        {
            bool flip = ctx.Generate(Conjecture.Core.Generate.Booleans());
            sb.Append(flip ? (char.IsUpper(ch) ? char.ToLower(ch) : char.ToUpper(ch)) : ch);
        }
        else
        {
            sb.Append(ch);
        }
    }

    private void GenerateCharClass(IGeneratorContext ctx, CharClass cc, StringBuilder sb)
    {
        IReadOnlyList<CharRange> ranges = cc.Ranges;

        if (ignoreCase)
        {
            ranges = ExpandCaseInsensitive(ranges);
        }

        if (cc.Negated)
        {
            IReadOnlyList<CharRange> complement = CharRangeHelpers.ComplementRanges(ranges, '\u0000', '\uFFFF');
            char ch = CharRangeHelpers.SampleFromRanges(complement, ctx);
            sb.Append(ch);
        }
        else
        {
            char ch = CharRangeHelpers.SampleFromRanges(ranges, ctx);
            sb.Append(ch);
        }
    }

    private static IReadOnlyList<CharRange> ExpandCaseInsensitive(IReadOnlyList<CharRange> ranges)
    {
        List<CharRange> expanded = [.. ranges];
        foreach (CharRange r in ranges)
        {
            // If this is a uniform-case letter range, emit the paired case as a single range.
            char lo = (char)r.Low;
            char hi = (char)r.High;
            if (lo == hi)
            {
                // Singleton — expand individually.
                if (char.IsLower(lo))
                {
                    expanded.Add(new CharRange(char.ToUpper(lo), char.ToUpper(lo)));
                }
                else if (char.IsUpper(lo))
                {
                    expanded.Add(new CharRange(char.ToLower(lo), char.ToLower(lo)));
                }
            }
            else if (char.IsLower(lo) && char.IsLower(hi))
            {
                // Contiguous lowercase range → add the paired uppercase range.
                expanded.Add(new CharRange(char.ToUpper(lo), char.ToUpper(hi)));
            }
            else if (char.IsUpper(lo) && char.IsUpper(hi))
            {
                // Contiguous uppercase range → add the paired lowercase range.
                expanded.Add(new CharRange(char.ToLower(lo), char.ToLower(hi)));
            }
            else
            {
                // Mixed / partial / non-letter range — expand char by char but coalesce.
                List<char> extras = [];
                for (int cp = r.Low; cp <= r.High; cp++)
                {
                    char c = (char)cp;
                    if (char.IsLower(c))
                    {
                        extras.Add(char.ToUpper(c));
                    }
                    else if (char.IsUpper(c))
                    {
                        extras.Add(char.ToLower(c));
                    }
                }

                // Coalesce adjacent extras into ranges.
                extras.Sort();
                for (int i = 0; i < extras.Count;)
                {
                    char start = extras[i];
                    char end = start;
                    while (i + 1 < extras.Count && extras[i + 1] == end + 1)
                    {
                        i++;
                        end = extras[i];
                    }

                    expanded.Add(new CharRange(start, end));
                    i++;
                }
            }
        }

        return expanded;
    }

    private void GenerateQuantifier(
        IGeneratorContext ctx,
        Quantifier q,
        StringBuilder sb,
        Dictionary<int, string> captures,
        Dictionary<string, string> namedCaptures)
    {
        const int extraSpan = 16;
        int maxCount = q.Max ?? (q.Min + extraSpan);
        int count = ctx.Generate(Conjecture.Core.Generate.Integers<int>(q.Min, maxCount));
        for (int i = 0; i < count; i++)
        {
            GenerateNode(ctx, q.Inner, sb, captures, namedCaptures);
        }
    }

    private void GenerateDot(IGeneratorContext ctx, StringBuilder sb)
    {
        if (singleline)
        {
            // Singleline: dot matches any char including newline.
            // Sample [0x00..0xFF] so '\n' appears with sufficient probability for fixed-seed tests.
            char c = (char)ctx.Generate(Conjecture.Core.Generate.Integers<int>(0x00, 0xFF));
            sb.Append(c);
        }
        else
        {
            // Non-singleline: dot matches any char except '\n'; sample full BMP then shift if needed.
            int cp = ctx.Generate(Conjecture.Core.Generate.Integers<int>(0x00, 0xFFFF));
            if (cp == '\n')
            {
                // Shift deterministically: move to next codepoint, wrapping away from '\n'.
                cp = cp == 0xFFFF ? 0xFFFE : cp + 1;
            }

            sb.Append((char)cp);
        }
    }

    private void GenerateUnicodeCategory(IGeneratorContext ctx, UnicodeCategory uc, StringBuilder sb)
    {
        if (genOptions.UnicodeCategories == UnicodeCoverage.Ascii)
        {
            char[] candidates = BuildAsciiCandidatesForGroup(uc.Category, uc.Negated);
            if (candidates.Length == 0)
            {
                sb.Append('a');
                return;
            }

            char picked = ctx.Generate(Conjecture.Core.Generate.SampledFrom(candidates));
            sb.Append(picked);
        }
        else
        {
            // Full BMP: precomputed candidate lists per category — avoids Assume-based rejection
            // which exhausts the filter budget for sparse categories (e.g. \p{Lt}).
            HashSet<System.Globalization.UnicodeCategory> targetCats = MapCategoryGroup(uc.Category);

            // Merge candidate arrays for all target categories into one list.
            List<char> allCandidates = [];
            foreach (System.Globalization.UnicodeCategory cat in targetCats)
            {
                if (BmpCandidates.TryGetValue(cat, out char[]? catChars))
                {
                    allCandidates.AddRange(catChars);
                }
            }

            if (uc.Negated)
            {
                // Build complement: all BMP chars not in targetCats.
                List<char> complement = [];
                foreach (KeyValuePair<System.Globalization.UnicodeCategory, char[]> kv in BmpCandidates)
                {
                    if (!targetCats.Contains(kv.Key))
                    {
                        complement.AddRange(kv.Value);
                    }
                }

                allCandidates = complement;
            }

            if (allCandidates.Count == 0)
            {
                sb.Append('a');
                return;
            }

            char picked = ctx.Generate(Conjecture.Core.Generate.SampledFrom(allCandidates));
            sb.Append(picked);
        }
    }

    private static char[] BuildAsciiCandidatesForGroup(string category, bool negated)
    {
        HashSet<System.Globalization.UnicodeCategory> targetCats = MapCategoryGroup(category);
        List<char> result = [];
        for (int i = 0; i < 128; i++)
        {
            char c = (char)i;
            System.Globalization.UnicodeCategory charCat = CharUnicodeInfo.GetUnicodeCategory(c);
            bool inGroup = targetCats.Contains(charCat);
            bool matches = negated ? !inGroup : inGroup;
            if (matches)
            {
                result.Add(c);
            }
        }

        return [.. result];
    }

    private static HashSet<System.Globalization.UnicodeCategory> MapCategoryGroup(string name)
    {
        return name switch
        {
            "L" => [
                System.Globalization.UnicodeCategory.UppercaseLetter,
                System.Globalization.UnicodeCategory.LowercaseLetter,
                System.Globalization.UnicodeCategory.TitlecaseLetter,
                System.Globalization.UnicodeCategory.ModifierLetter,
                System.Globalization.UnicodeCategory.OtherLetter,
            ],
            "Lu" => [System.Globalization.UnicodeCategory.UppercaseLetter],
            "Ll" => [System.Globalization.UnicodeCategory.LowercaseLetter],
            "Lt" => [System.Globalization.UnicodeCategory.TitlecaseLetter],
            "Lm" => [System.Globalization.UnicodeCategory.ModifierLetter],
            "Lo" => [System.Globalization.UnicodeCategory.OtherLetter],
            "N" => [
                System.Globalization.UnicodeCategory.DecimalDigitNumber,
                System.Globalization.UnicodeCategory.LetterNumber,
                System.Globalization.UnicodeCategory.OtherNumber,
            ],
            "Nd" => [System.Globalization.UnicodeCategory.DecimalDigitNumber],
            "Nl" => [System.Globalization.UnicodeCategory.LetterNumber],
            "No" => [System.Globalization.UnicodeCategory.OtherNumber],
            "P" => [
                System.Globalization.UnicodeCategory.ConnectorPunctuation,
                System.Globalization.UnicodeCategory.DashPunctuation,
                System.Globalization.UnicodeCategory.OpenPunctuation,
                System.Globalization.UnicodeCategory.ClosePunctuation,
                System.Globalization.UnicodeCategory.InitialQuotePunctuation,
                System.Globalization.UnicodeCategory.FinalQuotePunctuation,
                System.Globalization.UnicodeCategory.OtherPunctuation,
            ],
            "Pc" => [System.Globalization.UnicodeCategory.ConnectorPunctuation],
            "Pd" => [System.Globalization.UnicodeCategory.DashPunctuation],
            "Ps" => [System.Globalization.UnicodeCategory.OpenPunctuation],
            "Pe" => [System.Globalization.UnicodeCategory.ClosePunctuation],
            "Pi" => [System.Globalization.UnicodeCategory.InitialQuotePunctuation],
            "Pf" => [System.Globalization.UnicodeCategory.FinalQuotePunctuation],
            "Po" => [System.Globalization.UnicodeCategory.OtherPunctuation],
            "S" => [
                System.Globalization.UnicodeCategory.MathSymbol,
                System.Globalization.UnicodeCategory.CurrencySymbol,
                System.Globalization.UnicodeCategory.ModifierSymbol,
                System.Globalization.UnicodeCategory.OtherSymbol,
            ],
            "Sm" => [System.Globalization.UnicodeCategory.MathSymbol],
            "Sc" => [System.Globalization.UnicodeCategory.CurrencySymbol],
            "Sk" => [System.Globalization.UnicodeCategory.ModifierSymbol],
            "So" => [System.Globalization.UnicodeCategory.OtherSymbol],
            "Z" => [
                System.Globalization.UnicodeCategory.SpaceSeparator,
                System.Globalization.UnicodeCategory.LineSeparator,
                System.Globalization.UnicodeCategory.ParagraphSeparator,
            ],
            "Zs" => [System.Globalization.UnicodeCategory.SpaceSeparator],
            "Zl" => [System.Globalization.UnicodeCategory.LineSeparator],
            "Zp" => [System.Globalization.UnicodeCategory.ParagraphSeparator],
            "C" => [
                System.Globalization.UnicodeCategory.Control,
                System.Globalization.UnicodeCategory.Format,
                System.Globalization.UnicodeCategory.Surrogate,
                System.Globalization.UnicodeCategory.PrivateUse,
                System.Globalization.UnicodeCategory.OtherNotAssigned,
            ],
            "Cc" => [System.Globalization.UnicodeCategory.Control],
            "Cf" => [System.Globalization.UnicodeCategory.Format],
            "Cs" => [System.Globalization.UnicodeCategory.Surrogate],
            "Co" => [System.Globalization.UnicodeCategory.PrivateUse],
            "Cn" => [System.Globalization.UnicodeCategory.OtherNotAssigned],
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown Unicode category name."),
        };
    }
}