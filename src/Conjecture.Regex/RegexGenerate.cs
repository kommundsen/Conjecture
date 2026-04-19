// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Conjecture.Core;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Regex;

/// <summary>Factory methods for generating strings that match a regular expression.</summary>
public static class RegexGenerate
{
    private const int CacheCapacity = 256;

    // Cap-and-clear bounded cache: once the cap is reached, the whole dictionary is cleared.
    // Simple and allocation-free; true LRU is not required for this use case.
    private static readonly ConcurrentDictionary<string, DotNetRegex> Cache = new();

    // Matching(string) and Matching(Regex) are intentionally separate overloads; callers always
    // pass one or the other, so the 'ambiguous on optional parameter' warning does not apply.
#pragma warning disable RS0026 // multiple overloads with optional parameters
    /// <summary>Returns a strategy that generates strings matching <paramref name="pattern"/>.</summary>
    public static Strategy<string> Matching(string pattern, RegexGenOptions? options = null)
    {
        DotNetRegex regex = GetOrAddCached(pattern);
        return Matching(regex, options);
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
        return NotMatching(regex, options);
    }

    /// <summary>Returns a strategy that generates strings that do not match <paramref name="regex"/>.</summary>
    public static Strategy<string> NotMatching(DotNetRegex regex, RegexGenOptions? options = null)
    {
        return new NotMatchingStrategy(regex, regex.ToString(), options);
    }
#pragma warning restore RS0026

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