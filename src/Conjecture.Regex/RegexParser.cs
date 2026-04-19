// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Conjecture.Regex;

internal static class RegexParser
{
    internal static RegexNode Parse(string pattern, RegexOptions options)
    {
        _ = options;
        Parser parser = new(pattern);
        return parser.ParseAlternation();
    }

    private sealed class Parser(string pattern)
    {
        private static readonly IReadOnlyList<CharRange> WordRanges =
            [new('a', 'z'), new('A', 'Z'), new('0', '9'), new('_', '_')];

        private static readonly IReadOnlyList<CharRange> SpaceRanges =
            [new(' ', ' '), new('\t', '\t'), new('\n', '\n'), new('\r', '\r'), new('\f', '\f')];

        private static readonly IReadOnlyList<CharRange> DigitRanges =
            [new('0', '9')];

        private readonly string pat = pattern;
        private int pos = 0;
        private int captureCounter = 0;

        private bool AtEnd => pos >= pat.Length;

        private char Current => pat[pos];

        private char Consume()
        {
            char ch = pat[pos];
            pos++;
            return ch;
        }

        internal RegexNode ParseAlternation()
        {
            List<RegexNode> arms = [];
            arms.Add(ParseSequence());

            while (!AtEnd && Current == '|')
            {
                pos++;
                arms.Add(ParseSequence());
            }

            return arms.Count == 1 ? arms[0] : new Alternation(arms);
        }

        private RegexNode ParseSequence()
        {
            List<RegexNode> items = [];

            while (!AtEnd && Current != '|' && Current != ')')
            {
                RegexNode atom = ParseAtomWithQuantifier();
                items.Add(atom);
            }

            return items.Count == 1 ? items[0] : new Sequence(items);
        }

        private RegexNode ParseAtomWithQuantifier()
        {
            RegexNode atom = ParseAtom();
            return TryParseQuantifier(atom);
        }

        private RegexNode TryParseQuantifier(RegexNode inner)
        {
            if (AtEnd)
            {
                return inner;
            }

            int min;
            int? max;

            switch (Current)
            {
                case '*':
                    pos++;
                    min = 0;
                    max = null;
                    break;
                case '+':
                    pos++;
                    min = 1;
                    max = null;
                    break;
                case '?':
                    pos++;
                    min = 0;
                    max = 1;
                    break;
                case '{':
                    (min, max) = ParseBraceQuantifier();
                    break;
                default:
                    return inner;
            }

            bool lazy = !AtEnd && Current == '?';
            if (lazy)
            {
                pos++;
            }

            return new Quantifier(inner, min, max, lazy);
        }

        private (int min, int? max) ParseBraceQuantifier()
        {
            pos++;
            int min = ParseInt();

            if (!AtEnd && Current == '}')
            {
                pos++;
                return (min, min);
            }

            pos++;

            if (!AtEnd && Current == '}')
            {
                pos++;
                return (min, null);
            }

            int max = ParseInt();
            pos++;
            return (min, max);
        }

        private int ParseInt()
        {
            int start = pos;
            while (!AtEnd && char.IsDigit(Current))
            {
                pos++;
            }

            return int.Parse(pat.AsSpan(start, pos - start));
        }

        private RegexNode ParseAtom()
        {
            if (AtEnd)
            {
                throw new InvalidOperationException("Unexpected end of pattern");
            }

            char ch = Current;

            if (ch == '(')
            {
                return ParseGroup();
            }

            if (ch == '[')
            {
                return ParseCharClass();
            }

            if (ch == '\\')
            {
                return ParseEscape();
            }

            if (ch == '^')
            {
                pos++;
                return new Anchor(AnchorKind.Start);
            }

            if (ch == '$')
            {
                pos++;
                return new Anchor(AnchorKind.End);
            }

            if (ch == '.')
            {
                pos++;
                return new Dot();
            }

            pos++;
            return new Literal(ch);
        }

        private RegexNode ParseGroup()
        {
            pos++;

            if (!AtEnd && Current == '?')
            {
                pos++;

                if (!AtEnd && Current == '<')
                {
                    pos++;
                    if (!AtEnd && (Current == '=' || Current == '!'))
                    {
                        bool isPositive = Current == '=';
                        pos++;
                        RegexNode inner = ParseAlternation();
                        pos++;
                        return new LookaroundAssertion(inner, false, isPositive);
                    }
                    else
                    {
                        string name = ParseGroupName('>');
                        captureCounter++;
                        int captureIdx = captureCounter;
                        RegexNode inner = ParseAlternation();
                        pos++;
                        return new Group(inner, captureIdx, name);
                    }
                }

                if (!AtEnd && Current == '=')
                {
                    pos++;
                    RegexNode inner = ParseAlternation();
                    pos++;
                    return new LookaroundAssertion(inner, true, true);
                }

                if (!AtEnd && Current == '!')
                {
                    pos++;
                    RegexNode inner = ParseAlternation();
                    pos++;
                    return new LookaroundAssertion(inner, true, false);
                }

                if (!AtEnd && Current == ':')
                {
                    pos++;
                }

                RegexNode ncInner = ParseAlternation();
                pos++;
                return ncInner;
            }

            captureCounter++;
            int idx = captureCounter;
            RegexNode groupInner = ParseAlternation();
            pos++;
            return new Group(groupInner, idx, null);
        }

        private string ParseGroupName(char terminator)
        {
            int start = pos;
            while (!AtEnd && Current != terminator)
            {
                pos++;
            }

            string name = pat[start..pos];
            pos++;
            return name;
        }

        private RegexNode ParseCharClass()
        {
            pos++;

            bool negated = !AtEnd && Current == '^';
            if (negated)
            {
                pos++;
            }

            List<CharRange> ranges = [];

            while (!AtEnd && Current != ']')
            {
                if (Current == '\\')
                {
                    pos++;
                    char escaped = Consume();
                    IReadOnlyList<CharRange>? shorthandRanges = GetShorthandRanges(escaped);
                    if (shorthandRanges is not null)
                    {
                        foreach (CharRange r in shorthandRanges)
                        {
                            ranges.Add(r);
                        }
                    }
                    else
                    {
                        char low = UnescapeChar(escaped);
                        if (!AtEnd && Current == '-' && pos + 1 < pat.Length && pat[pos + 1] != ']')
                        {
                            pos++;
                            char next = Consume();
                            ranges.Add(new CharRange(low, next));
                        }
                        else
                        {
                            ranges.Add(new CharRange(low, low));
                        }
                    }
                }
                else
                {
                    char low = Consume();
                    if (!AtEnd && Current == '-' && pos + 1 < pat.Length && pat[pos + 1] != ']')
                    {
                        pos++;
                        char high = Consume();
                        ranges.Add(new CharRange(low, high));
                    }
                    else
                    {
                        ranges.Add(new CharRange(low, low));
                    }
                }
            }

            pos++;

            return new CharClass(ranges, negated);
        }

        private RegexNode ParseEscape()
        {
            pos++;
            if (AtEnd)
            {
                throw new InvalidOperationException("Unexpected end after backslash");
            }

            char ch = Consume();

            if (ch == 'b')
            {
                return new Anchor(AnchorKind.WordBoundary);
            }

            if (ch == 'B')
            {
                return new Anchor(AnchorKind.NonWordBoundary);
            }

            if (ch == 'd')
            {
                return new CharClass(DigitRanges, false);
            }

            if (ch == 'D')
            {
                return new CharClass(DigitRanges, true);
            }

            if (ch == 'w')
            {
                return new CharClass(WordRanges, false);
            }

            if (ch == 'W')
            {
                return new CharClass(WordRanges, true);
            }

            if (ch == 's')
            {
                return new CharClass(SpaceRanges, false);
            }

            if (ch == 'S')
            {
                return new CharClass(SpaceRanges, true);
            }

            if (ch == 'p' || ch == 'P')
            {
                bool negated = ch == 'P';
                pos++;
                int start = pos;
                while (!AtEnd && Current != '}')
                {
                    pos++;
                }

                string category = pat[start..pos];
                pos++;
                return new UnicodeCategory(category, negated);
            }

            if (ch == 'k')
            {
                pos++;
                string name = ParseGroupName('>');
                return new NamedBackreference(name);
            }

            if (char.IsDigit(ch))
            {
                int index = ch - '0';
                return new Backreference(index);
            }

            return new Literal(UnescapeChar(ch));
        }

        private static char UnescapeChar(char ch)
        {
            return ch switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'f' => '\f',
                _ => ch,
            };
        }

        private static IReadOnlyList<CharRange>? GetShorthandRanges(char ch)
        {
            return ch == 'd' ? DigitRanges : ch == 'w' ? WordRanges : ch == 's' ? SpaceRanges : null;
        }
    }
}