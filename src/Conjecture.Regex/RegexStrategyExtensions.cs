// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Conjecture.Regex;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Core;

/// <summary>Extension methods for generating strings that match (or do not match) a regular expression.</summary>
public static class RegexStrategyExtensions
{
    private const int CacheCapacity = 256;

    // Cap-and-clear bounded cache: once the cap is reached, the whole dictionary is cleared.
    // Simple and allocation-free; true LRU is not required for this use case.
    private static readonly ConcurrentDictionary<string, DotNetRegex> Cache = new();

    extension(Strategy)
    {
        // Matching(string) and Matching(Regex) are intentionally separate overloads; callers always
        // pass one or the other, so the 'ambiguous on optional parameter' warning does not apply.
#pragma warning disable RS0026 // multiple overloads with optional parameters
        /// <summary>Returns a strategy that generates strings matching <paramref name="pattern"/>.</summary>
        public static Strategy<string> Matching(string pattern, RegexGenOptions? options = null)
        {
            DotNetRegex regex = GetOrAddCached(pattern);
            return Strategy.Matching(regex, options);
        }

        /// <summary>Returns a strategy that generates strings matching <paramref name="regex"/>.</summary>
        public static Strategy<string> Matching(DotNetRegex regex, RegexGenOptions? options = null)
        {
            RegexGenOptions effectiveOptions = options ?? new();
            RegexNode root = RegexParser.Parse(regex.ToString(), regex.Options);
            return new MatchingStrategy(root, regex, regex.Options, effectiveOptions);
        }

        /// <summary>Returns a strategy that generates strings that do not match <paramref name="pattern"/>.</summary>
        public static Strategy<string> NotMatching(string pattern, RegexGenOptions? options = null)
        {
            DotNetRegex regex = GetOrAddCached(pattern);
            return Strategy.NotMatching(regex, options);
        }

        /// <summary>Returns a strategy that generates strings that do not match <paramref name="regex"/>.</summary>
        public static Strategy<string> NotMatching(DotNetRegex regex, RegexGenOptions? options = null)
        {
            return new NotMatchingStrategy(regex, regex.ToString(), options);
        }

        /// <summary>Returns a strategy that generates strings that may trigger ReDoS for <paramref name="pattern"/>.</summary>
        public static Strategy<string> ReDoSHunter(string pattern, int maxMatchMs = 5)
            => Strategy.ReDoSHunter(GetOrAddCached(pattern), maxMatchMs);

        /// <summary>Returns a strategy that generates strings that may trigger ReDoS for <paramref name="regex"/>.</summary>
        public static Strategy<string> ReDoSHunter(DotNetRegex regex, int maxMatchMs = 5)
        {
            if (regex.Options.HasFlag(RegexOptions.NonBacktracking))
            {
                return Strategy.Matching(regex).WithLabel("redos:non-backtracking");
            }

            RegexNode root = RegexParser.Parse(regex.ToString(), regex.Options);
            return new ReDoSHunterStrategy(root, regex, maxMatchMs);
        }
#pragma warning restore RS0026

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Email"/>.</summary>
        public static Strategy<string> Email() => Strategy.Matching(KnownRegex.Email);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.Email"/>.</summary>
        public static Strategy<string> NotEmail() => Strategy.NotMatching(KnownRegex.Email);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Url"/>.</summary>
        public static Strategy<string> Url() => Strategy.Matching(KnownRegex.Url);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.Url"/>.</summary>
        public static Strategy<string> NotUrl() => Strategy.NotMatching(KnownRegex.Url);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Uuid"/>.</summary>
        public static Strategy<string> Uuid() => Strategy.Matching(KnownRegex.Uuid);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.Uuid"/>.</summary>
        public static Strategy<string> NotUuid() => Strategy.NotMatching(KnownRegex.Uuid);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.IsoDate"/>.</summary>
        public static Strategy<string> IsoDate() => Strategy.Matching(KnownRegex.IsoDate);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.IsoDate"/>.</summary>
        public static Strategy<string> NotIsoDate() => Strategy.NotMatching(KnownRegex.IsoDate);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.CreditCard"/>.</summary>
        public static Strategy<string> CreditCard() => Strategy.Matching(KnownRegex.CreditCard);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.CreditCard"/>.</summary>
        public static Strategy<string> NotCreditCard() => Strategy.NotMatching(KnownRegex.CreditCard);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Ipv4"/>.</summary>
        public static Strategy<string> Ipv4() => Strategy.Matching(KnownRegex.Ipv4);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Ipv6"/>.</summary>
        public static Strategy<string> Ipv6() => Strategy.Matching(KnownRegex.Ipv6);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Date"/>.</summary>
        public static Strategy<string> Date() => Strategy.Matching(KnownRegex.Date);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Time"/>.</summary>
        public static Strategy<string> Time() => Strategy.Matching(KnownRegex.Time);
    }

    private static DotNetRegex GetOrAddCached(string pattern)
    {
        return Cache.GetOrAdd(pattern, static p =>
        {
            if (Cache.Count >= CacheCapacity)
            {
                Cache.Clear();
            }

            return new DotNetRegex(p);
        });
    }
}