// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Regex;

/// <summary>
/// Shared regex node dispatch logic used by both <see cref="MatchingStrategy"/> and
/// <see cref="ReDoSHunterStrategy"/>.  The only behavioural difference — how many
/// repetitions a <see cref="Quantifier"/> produces — is injected as a delegate.
/// </summary>
internal static class RegexNodeGenerator
{
    // Precomputed BMP candidates per Unicode category, shared across all users.
    internal static readonly Dictionary<System.Globalization.UnicodeCategory, char[]> BmpCandidates =
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

    internal static void GenerateNode(
        IGenerationContext ctx,
        RegexNode node,
        StringBuilder sb,
        Dictionary<int, string> captures,
        Dictionary<string, string> namedCaptures,
        Func<IGenerationContext, Quantifier, int> selectCount)
    {
        switch (node)
        {
            case Literal lit:
                sb.Append(lit.Ch);
                break;

            case CharClass cc:
                {
                    IReadOnlyList<CharRange> ranges = cc.Negated
                        ? CharRangeHelpers.ComplementRanges(cc.Ranges, '\u0000', '\uFFFF')
                        : cc.Ranges;
                    char ch = CharRangeHelpers.SampleFromRanges(ranges, ctx);
                    sb.Append(ch);
                    break;
                }

            case Quantifier q:
                {
                    int count = selectCount(ctx, q);
                    for (int i = 0; i < count; i++)
                    {
                        GenerateNode(ctx, q.Inner, sb, captures, namedCaptures, selectCount);
                    }

                    break;
                }

            case Alternation alt:
                {
                    int idx = ctx.Generate(Conjecture.Core.Generate.Integers<int>(0, alt.Arms.Count - 1));
                    GenerateNode(ctx, alt.Arms[idx], sb, captures, namedCaptures, selectCount);
                    break;
                }

            case Sequence seq:
                foreach (RegexNode item in seq.Items)
                {
                    GenerateNode(ctx, item, sb, captures, namedCaptures, selectCount);
                }

                break;

            case Group grp:
                {
                    int startPos = sb.Length;
                    GenerateNode(ctx, grp.Inner, sb, captures, namedCaptures, selectCount);
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
                break;

            case Dot:
                {
                    int cp = ctx.Generate(Conjecture.Core.Generate.Integers<int>(0x00, 0xFF));
                    sb.Append((char)cp);
                    break;
                }

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
                break;

            default:
                throw new NotSupportedException($"Unsupported node type: {node.GetType().Name}");
        }
    }

    internal static void GenerateUnicodeCategory(IGenerationContext ctx, UnicodeCategory uc, StringBuilder sb)
    {
        if (!CandidateCache.TryGetValue((uc.Category, uc.Negated), out char[]? candidates) || candidates.Length == 0)
        {
            sb.Append('a');
            return;
        }

        char picked = ctx.Generate(Conjecture.Core.Generate.SampledFrom(candidates));
        sb.Append(picked);
    }

    private static readonly Dictionary<string, HashSet<System.Globalization.UnicodeCategory>> CategoryGroupCache =
        BuildCategoryGroupCache();

    private static Dictionary<string, HashSet<System.Globalization.UnicodeCategory>> BuildCategoryGroupCache()
    {
        Dictionary<string, HashSet<System.Globalization.UnicodeCategory>> cache = [];
        string[] keys =
        [
            "L", "Lu", "Ll", "Lt", "Lm", "Lo",
            "N", "Nd", "Nl", "No",
            "P", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po",
            "S", "Sm", "Sc", "Sk", "So",
            "Z", "Zs", "Zl", "Zp",
            "C", "Cc", "Cf", "Cs", "Co", "Cn",
        ];
        foreach (string key in keys)
        {
            cache[key] = MapCategoryGroupCore(key);
        }

        return cache;
    }

    internal static readonly Dictionary<(string category, bool negated), char[]> CandidateCache =
        BuildCandidateCache();

    private static Dictionary<(string category, bool negated), char[]> BuildCandidateCache()
    {
        Dictionary<(string, bool), char[]> cache = [];
        string[] keys =
        [
            "L", "Lu", "Ll", "Lt", "Lm", "Lo",
            "N", "Nd", "Nl", "No",
            "P", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po",
            "S", "Sm", "Sc", "Sk", "So",
            "Z", "Zs", "Zl", "Zp",
            "C", "Cc", "Cf", "Cs", "Co", "Cn",
        ];
        foreach (string key in keys)
        {
            HashSet<System.Globalization.UnicodeCategory> targetCats = MapCategoryGroupCore(key);
            List<char> positive = [];
            List<char> complement = [];
            foreach (KeyValuePair<System.Globalization.UnicodeCategory, char[]> kv in BmpCandidates)
            {
                if (targetCats.Contains(kv.Key))
                {
                    positive.AddRange(kv.Value);
                }
                else
                {
                    complement.AddRange(kv.Value);
                }
            }

            cache[(key, false)] = [.. positive];
            cache[(key, true)] = [.. complement];
        }

        return cache;
    }

    internal static HashSet<System.Globalization.UnicodeCategory> MapCategoryGroup(string name) =>
        CategoryGroupCache.TryGetValue(name, out HashSet<System.Globalization.UnicodeCategory>? cats)
            ? cats
            : throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown Unicode category name.");

    private static HashSet<System.Globalization.UnicodeCategory> MapCategoryGroupCore(string name)
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