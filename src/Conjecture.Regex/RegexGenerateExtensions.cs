// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Conjecture.Regex;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Core;

/// <summary>Factory methods for generating strings that match (or do not match) a regular expression.</summary>
public static class RegexGenerateExtensions
{
    private const int CacheCapacity = 256;

    // Cap-and-clear bounded cache: once the cap is reached, the whole dictionary is cleared.
    // Simple and allocation-free; true LRU is not required for this use case.
    private static readonly ConcurrentDictionary<string, DotNetRegex> Cache = new();

    extension(Generate)
    {
        // Matching(string) and Matching(Regex) are intentionally separate overloads; callers always
        // pass one or the other, so the 'ambiguous on optional parameter' warning does not apply.
#pragma warning disable RS0026 // multiple overloads with optional parameters
        /// <summary>Returns a strategy that generates strings matching <paramref name="pattern"/>.</summary>
        public static Strategy<string> Matching(string pattern, RegexGenOptions? options = null)
        {
            DotNetRegex regex = GetOrAddCached(pattern);
            return Generate.Matching(regex, options);
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
            return Generate.NotMatching(regex, options);
        }

        /// <summary>Returns a strategy that generates strings that do not match <paramref name="regex"/>.</summary>
        public static Strategy<string> NotMatching(DotNetRegex regex, RegexGenOptions? options = null)
        {
            return new NotMatchingStrategy(regex, regex.ToString(), options);
        }

        /// <summary>Returns a strategy that generates strings that may trigger ReDoS for <paramref name="pattern"/>.</summary>
        public static Strategy<string> ReDoSHunter(string pattern, int maxMatchMs = 5)
            => Generate.ReDoSHunter(GetOrAddCached(pattern), maxMatchMs);

        /// <summary>Returns a strategy that generates strings that may trigger ReDoS for <paramref name="regex"/>.</summary>
        public static Strategy<string> ReDoSHunter(DotNetRegex regex, int maxMatchMs = 5)
        {
            if (regex.Options.HasFlag(RegexOptions.NonBacktracking))
            {
                return new DelegatingStrategy<string>(Generate.Matching(regex), "redos:non-backtracking");
            }

            RegexNode root = RegexParser.Parse(regex.ToString(), regex.Options);
            return new ReDoSHunterStrategy(root, regex, maxMatchMs);
        }
#pragma warning restore RS0026

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Email"/>.</summary>
        public static Strategy<string> Email() => Generate.Matching(KnownRegex.Email);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.Email"/>.</summary>
        public static Strategy<string> NotEmail() => Generate.NotMatching(KnownRegex.Email);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Url"/>.</summary>
        public static Strategy<string> Url() => Generate.Matching(KnownRegex.Url);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.Url"/>.</summary>
        public static Strategy<string> NotUrl() => Generate.NotMatching(KnownRegex.Url);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.Uuid"/>.</summary>
        public static Strategy<string> Uuid() => Generate.Matching(KnownRegex.Uuid);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.Uuid"/>.</summary>
        public static Strategy<string> NotUuid() => Generate.NotMatching(KnownRegex.Uuid);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.IsoDate"/>.</summary>
        public static Strategy<string> IsoDate() => Generate.Matching(KnownRegex.IsoDate);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.IsoDate"/>.</summary>
        public static Strategy<string> NotIsoDate() => Generate.NotMatching(KnownRegex.IsoDate);

        /// <summary>Returns a strategy that generates strings matching <see cref="KnownRegex.CreditCard"/>.</summary>
        public static Strategy<string> CreditCard() => Generate.Matching(KnownRegex.CreditCard);

        /// <summary>Returns a strategy that generates strings that do not match <see cref="KnownRegex.CreditCard"/>.</summary>
        public static Strategy<string> NotCreditCard() => Generate.NotMatching(KnownRegex.CreditCard);
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