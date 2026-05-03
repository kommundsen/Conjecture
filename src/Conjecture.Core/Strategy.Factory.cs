// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Conjecture.Core;

/// <summary>Static methods for creating and composing Conjecture strategies.</summary>
public static class Strategy
{
    /// <summary>Creates a strategy from an imperative body using <see cref="IGenerationContext"/>.</summary>
    public static Strategy<T> Compose<T>(Func<IGenerationContext, T> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new ComposeStrategy<T>(body);
    }

    /// <summary>Returns a strategy that generates random <see cref="bool"/> values.</summary>
    public static Strategy<bool> Booleans() => new BooleanStrategy();

    /// <summary>Returns a strategy that generates random <typeparamref name="T"/> values across the full range of the type.</summary>
    public static Strategy<T> Integers<T>() where T : IBinaryInteger<T>, IMinMaxValue<T>
        => new IntegerStrategy<T>(T.MinValue, T.MaxValue);

    /// <summary>Returns a strategy that generates random <typeparamref name="T"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<T> Integers<T>(T min, T max) where T : IBinaryInteger<T>
        => new IntegerStrategy<T>(min, max);

    /// <summary>Returns a strategy that generates random <see cref="BigInteger"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<BigInteger> Integers(BigInteger min, BigInteger max)
        => new BigIntegerStrategy(min, max);

    /// <summary>Returns a strategy that generates <typeparamref name="T"/> arrays with length in [<paramref name="minSize"/>, <paramref name="maxSize"/>] and elements drawn from <paramref name="inner"/>.</summary>
    public static Strategy<T[]> Arrays<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new ArrayStrategy<T>(inner, minSize, maxSize);
    }

    /// <summary>Returns a strategy that always produces <paramref name="value"/>.</summary>
    public static Strategy<T> Just<T>(T value) => new JustStrategy<T>(value);

    /// <summary>Returns a strategy that picks uniformly among <paramref name="strategies"/>.</summary>
    public static Strategy<T> OneOf<T>(params Strategy<T>[] strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        return new OneOfStrategy<T>(strategies);
    }

    /// <summary>Returns a strategy that picks uniformly among <paramref name="strategies"/>. Stack-allocated call sites avoid heap-array allocation.</summary>
    public static Strategy<T> OneOf<T>(params ReadOnlySpan<Strategy<T>> strategies)
    {
        if (strategies.IsEmpty)
        {
            throw new ArgumentException("At least one strategy is required.", nameof(strategies));
        }

        Strategy<T>[] copy = strategies.ToArray();
        return new OneOfStrategy<T>(copy);
    }

    /// <summary>Returns a strategy that picks uniformly from <paramref name="values"/>.</summary>
    public static Strategy<T> SampledFrom<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new SampledFromStrategy<T>(values);
    }

    /// <summary>Returns a strategy that generates random <typeparamref name="T"/> enum values.</summary>
    public static Strategy<T> Enums<T>() where T : struct, Enum => SampledFrom(Enum.GetValues<T>());

    /// <summary>Returns a strategy that generates random <see cref="double"/> values across the full range.</summary>
    public static Strategy<double> Doubles() => new FloatingPointStrategy<double>();

    /// <summary>Returns a strategy that generates random <see cref="double"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<double> Doubles(double min, double max) => new FloatingPointStrategy<double>(min, max);

    /// <summary>Returns a strategy that generates random <see cref="float"/> values across the full range.</summary>
    public static Strategy<float> Floats() => new FloatingPointStrategy<float>();

    /// <summary>Returns a strategy that generates random <see cref="float"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<float> Floats(float min, float max) => new FloatingPointStrategy<float>(min, max);

    /// <summary>Returns a strategy that generates random <see cref="Half"/> values across the full range.</summary>
    public static Strategy<Half> Halves() => new FloatingPointStrategy<Half>();

    /// <summary>Returns a strategy that generates random <see cref="Half"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<Half> Halves(Half min, Half max) => new FloatingPointStrategy<Half>(min, max);

    /// <summary>Returns a strategy that generates random strings. When <paramref name="alphabet"/> is provided it takes precedence and <paramref name="minCodepoint"/>/<paramref name="maxCodepoint"/> are ignored.</summary>
    public static Strategy<string> Strings(int minLength = 0, int maxLength = 20, int minCodepoint = 32, int maxCodepoint = 126, string? alphabet = null)
        => alphabet is not null
            ? new StringStrategy(alphabet, minLength, maxLength)
            : new StringStrategy(minLength, maxLength, minCodepoint, maxCodepoint);

    /// <summary>Returns a strategy that produces nullable <typeparamref name="T"/> values, with ~10% null probability.</summary>
    public static Strategy<T?> Nullable<T>(Strategy<T> inner) where T : struct
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new NullableStrategy<T>(inner);
    }

    /// <summary>Returns a strategy that produces <see cref="ValueTuple{T1,T2}"/> tuples from two component strategies.</summary>
    public static Strategy<(T1, T2)> Tuples<T1, T2>(Strategy<T1> first, Strategy<T2> second) => first.Zip(second);

    /// <summary>Returns a strategy that produces 3-element tuples from three component strategies.</summary>
    public static Strategy<(T1, T2, T3)> Tuples<T1, T2, T3>(Strategy<T1> first, Strategy<T2> second, Strategy<T3> third)
        => first.Zip(second).Zip(third, (ab, c) => (ab.Item1, ab.Item2, Item3: c));

    /// <summary>Returns a strategy that produces 4-element tuples from four component strategies.</summary>
    public static Strategy<(T1, T2, T3, T4)> Tuples<T1, T2, T3, T4>(Strategy<T1> first, Strategy<T2> second, Strategy<T3> third, Strategy<T4> fourth)
        => Tuples(first, second, third).Zip(fourth, (abc, d) => (abc.Item1, abc.Item2, abc.Item3, d));

    /// <summary>Returns a strategy that generates <see cref="List{T}"/> with size in [<paramref name="minSize"/>, <paramref name="maxSize"/>].</summary>
    public static Strategy<List<T>> Lists<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new ListStrategy<T>(inner, minSize, maxSize);
    }

    /// <summary>Returns a strategy that generates <see cref="IReadOnlySet{T}"/> with unique elements and size in [<paramref name="minSize"/>, <paramref name="maxSize"/>].</summary>
    public static Strategy<IReadOnlySet<T>> Sets<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new SetStrategy<T>(inner, minSize, maxSize);
    }

    /// <summary>Returns a strategy that generates <see cref="IReadOnlyDictionary{TKey,TValue}"/> with unique keys and size in [<paramref name="minSize"/>, <paramref name="maxSize"/>].</summary>
    public static Strategy<IReadOnlyDictionary<TKey, TValue>> Dictionaries<TKey, TValue>(Strategy<TKey> keyStrategy, Strategy<TValue> valueStrategy, int minSize = 0, int maxSize = 100)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keyStrategy);
        ArgumentNullException.ThrowIfNull(valueStrategy);
        return new DictionaryStrategy<TKey, TValue>(keyStrategy, valueStrategy, minSize, maxSize);
    }

    /// <summary>Returns a strategy that generates recursive data structures up to <paramref name="maxDepth"/> levels deep.</summary>
    /// <typeparam name="T">The type of value to generate.</typeparam>
    /// <param name="baseCase">Strategy for leaf nodes (depth 0). Used whenever the target depth is exhausted.</param>
    /// <param name="recursive">
    ///   Factory that receives a <c>self</c> strategy (which recurses at depth − 1) and returns a strategy
    ///   for non-leaf nodes. The engine substitutes <paramref name="baseCase"/> for <c>self</c> at depth 0.
    /// </param>
    /// <param name="maxDepth">Maximum recursion depth. Generated values have depth in [0, maxDepth]. Must be ≥ 0.</param>
    /// <returns>
    ///   A <see cref="Strategy{T}"/> whose generated values have depth at most <paramref name="maxDepth"/>.
    ///   The depth node is an IR integer, so the shrinker shrinks it toward 0, producing shallower structures
    ///   when shrinking a counterexample.
    /// </returns>
    /// <example>
    /// <code>
    /// // Expression tree: Literal | Add | Mul, up to 5 levels deep
    /// Strategy&lt;Expr&gt; exprStrategy = Strategy.Recursive&lt;Expr&gt;(
    ///     baseCase: Strategy.Integers&lt;int&gt;(0, 100).Select(n =&gt; (Expr)new Literal(n)),
    ///     recursive: self =&gt; Strategy.OneOf(
    ///         Strategy.Integers&lt;int&gt;(0, 100).Select(n =&gt; (Expr)new Literal(n)),
    ///         Strategy.Tuples(self, self).Select(t =&gt; (Expr)new Add(t.Item1, t.Item2)),
    ///         Strategy.Tuples(self, self).Select(t =&gt; (Expr)new Mul(t.Item1, t.Item2))),
    ///     maxDepth: 5);
    /// </code>
    /// </example>
    public static Strategy<T> Recursive<T>(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int maxDepth = 5)
    {
        ArgumentNullException.ThrowIfNull(baseCase);
        ArgumentNullException.ThrowIfNull(recursive);
        return new RecursiveStrategy<T>(baseCase, recursive, maxDepth);
    }

    /// <summary>Returns a strategy that generates <see cref="StateMachineRun{TState}"/> values by running <typeparamref name="TMachine"/> for up to <paramref name="maxSteps"/> steps.</summary>
    /// <typeparam name="TMachine">
    ///   The state machine type. Must implement <see cref="IStateMachine{TState, TCommand}"/>
    ///   and expose a public parameterless constructor.
    /// </typeparam>
    /// <typeparam name="TState">The type representing the system's state.</typeparam>
    /// <typeparam name="TCommand">The type representing a command that can be applied to the state.</typeparam>
    /// <param name="maxSteps">Maximum number of commands to generate per run. Defaults to 50.</param>
    /// <returns>
    ///   A <see cref="Strategy{T}"/> that produces <see cref="StateMachineRun{TState}"/> values.
    ///   If <see cref="IStateMachine{TState, TCommand}.Invariant"/> throws during generation, the strategy
    ///   propagates the exception so the enclosing property fails with a shrunk command sequence.
    /// </returns>
    /// <example>
    /// <code>
    /// public sealed class CounterMachine : IStateMachine&lt;int, string&gt;
    /// {
    ///     public int InitialState() =&gt; 0;
    ///     public IEnumerable&lt;Strategy&lt;string&gt;&gt; Commands(int state) =&gt; [Strategy.Just("inc")];
    ///     public int RunCommand(int state, string cmd) =&gt; state + 1;
    ///     public void Invariant(int state)
    ///     {
    ///         if (state &lt; 0)
    ///             throw new InvalidOperationException("counter went negative");
    ///     }
    /// }
    ///
    /// Strategy&lt;StateMachineRun&lt;int&gt;&gt; strategy =
    ///     Strategy.StateMachine&lt;CounterMachine, int, string&gt;(maxSteps: 50);
    /// </code>
    /// </example>
    public static Strategy<StateMachineRun<TState>> StateMachine<TMachine, TState, TCommand>(int maxSteps = 50)
        where TMachine : IStateMachine<TState, TCommand>, new()
    {
        return new StateMachineStrategy<TMachine, TState, TCommand>(maxSteps);
    }

    /// <summary>Returns a strategy that generates random <see cref="DateTimeOffset"/> values across the full range.</summary>
    public static Strategy<DateTimeOffset> DateTimeOffsets()
        => new DateTimeOffsetStrategy(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

    /// <summary>Returns a strategy that generates random <see cref="DateTimeOffset"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<DateTimeOffset> DateTimeOffsets(DateTimeOffset min, DateTimeOffset max)
        => new DateTimeOffsetStrategy(min, max);

    /// <summary>Returns a strategy that generates random <see cref="TimeSpan"/> values across the full range.</summary>
    public static Strategy<TimeSpan> TimeSpans()
        => new TimeSpanStrategy(TimeSpan.MinValue, TimeSpan.MaxValue);

    /// <summary>Returns a strategy that generates random <see cref="TimeSpan"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<TimeSpan> TimeSpans(TimeSpan min, TimeSpan max)
        => new TimeSpanStrategy(min, max);

    /// <summary>Returns a strategy that generates random <see cref="DateOnly"/> values across the full range.</summary>
    public static Strategy<DateOnly> DateOnlyValues()
        => new DateOnlyStrategy(DateOnly.MinValue, DateOnly.MaxValue);

    /// <summary>Returns a strategy that generates random <see cref="DateOnly"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<DateOnly> DateOnlyValues(DateOnly min, DateOnly max)
        => new DateOnlyStrategy(min, max);

    /// <summary>Returns a strategy that generates random <see cref="TimeOnly"/> values across the full range.</summary>
    public static Strategy<TimeOnly> TimeOnlyValues()
        => new TimeOnlyStrategy(TimeOnly.MinValue, TimeOnly.MaxValue);

    /// <summary>Returns a strategy that generates random <see cref="TimeOnly"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<TimeOnly> TimeOnlyValues(TimeOnly min, TimeOnly max)
        => new TimeOnlyStrategy(min, max);

    /// <summary>Returns a strategy that generates identifier strings of the form <c>[a-z]+\d+</c>. The alpha prefix is generated via IR string nodes so <c>StringAwarePass</c> can shrink it toward 'a', and the digit suffix is generated so <c>NumericAwareShrinkPass</c> can shrink it.</summary>
    public static Strategy<string> Identifiers(
        int minPrefixLength = 1,
        int maxPrefixLength = 6,
        int minDigits = 1,
        int maxDigits = 4)
        => new IdentifierStrategy(minPrefixLength, maxPrefixLength, minDigits, maxDigits);

    /// <summary>Returns a strategy that generates strings of the form <c>[prefix][digits][suffix]</c> where the digit part is generated via IR string nodes so <c>NumericAwareShrinkPass</c> can shrink it.</summary>
    public static Strategy<string> NumericStrings(
        int minDigits = 1,
        int maxDigits = 6,
        string? prefix = null,
        string? suffix = null)
        => new NumericStringStrategy(minDigits, maxDigits, prefix, suffix);

    /// <summary>Returns a strategy that generates version strings of the form <c>MAJOR.MINOR.PATCH</c> where each component is a numeric string generated via IR string nodes so <c>NumericAwareShrinkPass</c> can shrink each segment independently.</summary>
    public static Strategy<string> VersionStrings(
        int maxMajor = 9,
        int maxMinor = 9,
        int maxPatch = 9)
        => new VersionStringStrategy(maxMajor, maxMinor, maxPatch);

    /// <summary>Returns a strategy that generates DNS-like host names: 1..<paramref name="maxLabels"/> labels of lowercase alphanumerics joined by '.', with a TLD-shaped final label (lowercase letters only, length >= 2).</summary>
    public static Strategy<string> Hosts(int minLabels = 1, int maxLabels = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minLabels, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLabels, minLabels);
        return new HostStrategy(minLabels, maxLabels);
    }

    /// <summary>
    /// Creates a strategy that replays values from a fixed byte array (IR replay source).
    /// Useful for deterministic seed replay and round-trip testing.
    /// </summary>
    public static Strategy<T> FromBytes<T>(ReadOnlySpan<byte> buffer)
        => new Internal.FromBytesStrategy<T>(buffer, Internal.SharedParameterStrategyResolver.GetDefault<T>());

    /// <summary>Returns a strategy that generates random <see cref="Guid"/> values.</summary>
    public static Strategy<Guid> Guids() => new GuidStrategy();

    /// <summary>Returns a strategy that generates <see cref="Version"/> values with components in the configured ranges. Components default to a small range so shrinking converges quickly.</summary>
    public static Strategy<Version> Versions(int maxMajor = 9, int maxMinor = 9, int maxBuild = 9, int maxRevision = 9)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxMajor);
        ArgumentOutOfRangeException.ThrowIfNegative(maxMinor);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBuild);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRevision);
        return new VersionStrategy(maxMajor, maxMinor, maxBuild, maxRevision);
    }

    /// <summary>Returns a strategy that generates random <see cref="decimal"/> values.</summary>
    public static Strategy<decimal> Decimals() => new DecimalStrategy();

    /// <summary>Returns a strategy that generates random <see cref="decimal"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<decimal> Decimals(decimal min, decimal max) => new DecimalStrategy(min, max);

    /// <summary>Returns a strategy that generates random <see cref="DateTime"/> values across the full range.</summary>
    public static Strategy<DateTime> DateTimes()
        => new DateTimeStrategy(DateTime.MinValue, DateTime.MaxValue);

    /// <summary>Returns a strategy that generates random <see cref="DateTime"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<DateTime> DateTimes(DateTime min, DateTime max)
        => new DateTimeStrategy(min, max);

    /// <summary>Returns a strategy that generates random <see cref="char"/> values across the full Unicode range.</summary>
    public static Strategy<char> Chars() => new CharStrategy();

    /// <summary>Returns a strategy that generates <see cref="System.Net.IPAddress"/> values for the specified address family.</summary>
    /// <param name="kind">Which address families to generate. Must not be zero.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="kind"/> has no flags set.</exception>
    public static Strategy<System.Net.IPAddress> IPAddresses(IPAddressKind kind = IPAddressKind.Both)
        => kind == 0
            ? throw new ArgumentException("At least one IPAddressKind flag must be set.", nameof(kind))
            : new IPAddressStrategy(kind);

    /// <summary>Returns a strategy that generates <see cref="System.Net.IPEndPoint"/> values composed from <paramref name="addresses"/> and <paramref name="ports"/>.</summary>
    /// <param name="addresses">Address strategy; defaults to <c>IPAddresses(IPAddressKind.Both)</c>.</param>
    /// <param name="ports">Port strategy; defaults to <c>Integers&lt;int&gt;(0, 65535)</c>.</param>
    public static Strategy<System.Net.IPEndPoint> IPEndPoints(Strategy<System.Net.IPAddress>? addresses = null, Strategy<int>? ports = null)
        => new IPEndPointStrategy(
            addresses ?? IPAddresses(),
            ports ?? Integers<int>(0, 65535));

    /// <summary>Returns a strategy that generates <see cref="Uri"/> values of the requested <paramref name="kind"/>, optionally restricted to <paramref name="schemes"/>.</summary>
    public static Strategy<Uri> Uris(UriKind kind = UriKind.Absolute, IReadOnlyList<string>? schemes = null)
    {
        IReadOnlyList<string> resolvedSchemes = schemes ?? ["http", "https"];
        return resolvedSchemes.Count == 0
            ? throw new ArgumentException("At least one scheme is required.", nameof(schemes))
            : new UriStrategy(kind, resolvedSchemes);
    }

    /// <summary>Returns a strategy that generates <see cref="System.Net.Mail.MailAddress"/> values with locally-generated user and host parts.</summary>
    public static Strategy<System.Net.Mail.MailAddress> EmailAddresses() => new MailAddressStrategy();

    /// <summary>Returns a strategy that generates RFC 5321-shaped email address strings.</summary>
    public static Strategy<string> EmailAddressStrings() => new EmailAddressStringStrategy();

    /// <summary>Returns a strategy for <typeparamref name="T"/> using its registered <see cref="IStrategyProvider{T}"/>. The type must be decorated with <c>[Arbitrary]</c>.</summary>
    public static Strategy<T> For<T>() => GenerateForRegistry.Resolve<T>();

    /// <summary>Returns a strategy for <typeparamref name="T"/> with property overrides applied via <paramref name="configure"/>. The type must be decorated with <c>[Arbitrary]</c>.</summary>
    public static Strategy<T> For<T>(Action<ForConfiguration<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ForConfiguration<T> cfg = new();
        configure(cfg);
        return Compose<T>(ctx => ctx.Generate(GenerateForRegistry.ResolveWithOverrides(cfg)));
    }

    private static readonly ConcurrentDictionary<CultureTypes, CultureInfo[]> CulturesCache = new();

    /// <summary>Returns a strategy that generates random <see cref="CultureInfo"/> values from all cultures, with <see cref="CultureInfo.InvariantCulture"/> at index 0 to guide shrinking.</summary>
    public static Strategy<CultureInfo> Cultures()
        => SampledFrom(CulturesCache.GetOrAdd(CultureTypes.AllCultures, static t => BuildCultures(t)));

    /// <summary>Returns a strategy that generates random <see cref="CultureInfo"/> values matching <paramref name="types"/>, with <see cref="CultureInfo.InvariantCulture"/> at index 0 to guide shrinking.</summary>
    public static Strategy<CultureInfo> Cultures(CultureTypes types)
        => SampledFrom(CulturesCache.GetOrAdd(types, static t => BuildCultures(t)));

    private static CultureInfo[] BuildCultures(CultureTypes types)
    {
        List<CultureInfo> result = [CultureInfo.InvariantCulture];
        foreach (CultureInfo culture in CultureInfo.GetCultures(types))
        {
            if (culture.Name != CultureInfo.InvariantCulture.Name)
            {
                result.Add(culture);
            }
        }

        return [.. result];
    }
}