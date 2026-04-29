// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.RegularExpressions;

using Conjecture.Core;
using Conjecture.Core.Internal;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Regex;

internal sealed class NotMatchingStrategy : Strategy<string>
{
    private const int MaxAttempts = 100;
    private const int FallbackAttempts = 50;

    private enum Op { Insert, Delete, Replace }

    private readonly DotNetRegex regex;
    private readonly MatchingStrategy matchingStrategy;

    internal NotMatchingStrategy(DotNetRegex regex, string pattern, RegexGenOptions? options)
    {
        this.regex = regex;
        RegexGenOptions effectiveOptions = options ?? new();
        RegexNode root = RegexParser.Parse(pattern, regex.Options);
        matchingStrategy = new MatchingStrategy(root, regex, regex.Options, effectiveOptions);
    }

    internal override string Generate(ConjectureData data)
    {
        return Conjecture.Core.Strategy.Compose<string>(ctx =>
        {
            string candidate = ctx.Generate(matchingStrategy);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                string mutated = Mutate(ctx, candidate);
                if (!regex.IsMatch(mutated))
                {
                    return mutated;
                }
            }

            Strategy<string> fallback = Conjecture.Core.Strategy.Strings(minLength: 0, maxLength: 32);
            for (int i = 0; i < FallbackAttempts; i++)
            {
                string s = ctx.Generate(fallback);
                if (!regex.IsMatch(s))
                {
                    return s;
                }
            }

            data.MarkInvalid();
            throw new UnsatisfiedAssumptionException();
        }).Generate(data);
    }

    private string Mutate(IGenerationContext ctx, string s)
    {
        Op[] ops = [Op.Insert, Op.Delete, Op.Replace];
        Op op = ctx.Generate(Conjecture.Core.Strategy.SampledFrom(ops));

        if (op == Op.Delete && s.Length == 0)
        {
            op = Op.Insert;
        }

        if (op == Op.Replace && s.Length == 0)
        {
            op = Op.Insert;
        }

        return op switch
        {
            Op.Insert => DoInsert(ctx, s),
            Op.Delete => DoDelete(ctx, s),
            Op.Replace => DoReplace(ctx, s),
            _ => s,
        };
    }

    private static string DoInsert(IGenerationContext ctx, string s)
    {
        int index = ctx.Generate(Conjecture.Core.Strategy.Integers<int>(0, s.Length));
        char ch = (char)ctx.Generate(Conjecture.Core.Strategy.Integers<int>(0x20, 0x7E));
        return s.Insert(index, new string(ch, 1));
    }

    private static string DoDelete(IGenerationContext ctx, string s)
    {
        int index = ctx.Generate(Conjecture.Core.Strategy.Integers<int>(0, s.Length - 1));
        return s.Remove(index, 1);
    }

    private static string DoReplace(IGenerationContext ctx, string s)
    {
        int index = ctx.Generate(Conjecture.Core.Strategy.Integers<int>(0, s.Length - 1));
        int charCode = ctx.Generate(Conjecture.Core.Strategy.Integers<int>(0x20, 0x7E));
        if (charCode == s[index])
        {
            charCode ^= 1;
            // Clamp to printable range.
            if (charCode < 0x20)
            {
                charCode = 0x21;
            }

            if (charCode > 0x7E)
            {
                charCode = 0x7D;
            }
        }

        char[] chars = s.ToCharArray();
        chars[index] = (char)charCode;
        return new string(chars);
    }
}