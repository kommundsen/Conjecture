// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using Conjecture.Core;
using Conjecture.Core.Internal;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Regex;

internal sealed class ReDoSHunterStrategy(
    RegexNode root,
    DotNetRegex regex,
    int maxMatchMs) : Strategy<string>("redos:hunter")
{
    private const int AdversarialCap = 32;
    private const int InnerCap = 4;
    private const int Trials = 3;
    private const int HitsRequired = 2;

    private readonly Strategy<string>? fallback = BuildFallback(root, regex);
    private readonly DotNetRegex timedRegex = new(
        regex.ToString(),
        regex.Options,
        TimeSpan.FromMilliseconds(maxMatchMs));

    private static Strategy<string>? BuildFallback(RegexNode root, DotNetRegex regex)
    {
        return !NestedQuantifierDetector.HasNestedQuantifiers(root)
            ? Conjecture.Core.Strategy.Matching(regex).WithLabel("redos:no-nested-quantifiers")
            : (Strategy<string>?)null;
    }

    internal override string Generate(ConjectureData data)
    {
        return fallback is not null
            ? fallback.Generate(data)
            : Conjecture.Core.Strategy.Compose<string>(ctx =>
        {
            StringBuilder sb = new();
            Dictionary<int, string> captures = [];
            Dictionary<string, string> namedCaptures = [];
            RegexNodeGenerator.GenerateNode(ctx, root, sb, captures, namedCaptures, SelectCount);
            sb.Append('\x00');
            string candidate = sb.ToString();

            int hits = 0;
            Stopwatch sw = new();
            for (int trial = 0; trial < Trials; trial++)
            {
                sw.Restart();
                try
                {
                    timedRegex.IsMatch(candidate);
                    sw.Stop();
                    ctx.Target(sw.Elapsed.TotalMilliseconds, "timing");
                }
                catch (RegexMatchTimeoutException)
                {
                    hits++;
                }
            }

            if (hits >= HitsRequired)
            {
                data.MarkInteresting();
            }

            return candidate;
        }).Generate(data);
    }

    private static int SelectCount(IGenerationContext ctx, Quantifier q)
    {
        bool isAdversarial = q.Inner switch
        {
            Quantifier => true,
            Alternation => true,
            Group grp => ContainsQuantifierOrAlternation(grp.Inner),
            _ => false,
        };

        int cap = isAdversarial ? AdversarialCap : InnerCap;
        int maxCount = q.Max ?? (q.Min + cap);

        return ctx.Generate(Conjecture.Core.Strategy.Integers<int>(q.Min, maxCount));
    }

    private static bool ContainsQuantifierOrAlternation(RegexNode node)
    {
        return node switch
        {
            Quantifier => true,
            Alternation => true,
            Group grp => ContainsQuantifierOrAlternation(grp.Inner),
            Sequence seq => ContainsQuantifierOrAlternationInList(seq.Items),
            _ => false,
        };
    }

    private static bool ContainsQuantifierOrAlternationInList(IReadOnlyList<RegexNode> items)
    {
        foreach (RegexNode item in items)
        {
            if (ContainsQuantifierOrAlternation(item))
            {
                return true;
            }
        }

        return false;
    }
}