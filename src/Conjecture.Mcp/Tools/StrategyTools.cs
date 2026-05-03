// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.ComponentModel;

using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tools;

[McpServerToolType]
internal static class StrategyTools
{
    [McpServerTool(Name = "suggest-strategy")]
    [Description(
        "Suggests the Conjecture Strategy.* strategy to use for a given C# type or description. " +
        "Handles primitives (int, bool, string, float, double, byte[]), date/time types " +
        "(DateTimeOffset, TimeSpan, DateOnly, TimeOnly), collections " +
        "(List<T>, IReadOnlySet<T>, IReadOnlyDictionary<K,V>), nullable types, value " +
        "tuples, enums, and custom types. Also handles regex-constrained strings via the " +
        "Conjecture.Regex package: pass 'Regex' for pattern-matching strategies, 'ReDoS' or " +
        "'backtracking' for adversarial ReDoS-hunting strategies, or a description keyword " +
        "such as 'email' for curated RegexGenerate helpers. " +
        "For sealed abstract class hierarchies, use the suggest-strategy-for-sealed-hierarchy tool instead. " +
        "If the target type is decorated with `[Arbitrary]`, set `hasArbitraryAttribute: true` to get a `Strategy.For<T>()` recommendation instead of manual composition.")]
    public static string SuggestStrategy(
        [Description("The C# type name, e.g. 'int', 'string', 'List<int>', 'MyEnum', 'MyRecord'")] string typeName,
        [Description("Set to true when the target type is decorated with [Arbitrary]; returns a Strategy.For<T>() recommendation with optional property override DSL instead of manual composition.")] bool hasArbitraryAttribute = false)
    {
        return SuggestForType(typeName.Trim(), hasArbitraryAttribute);
    }

    [McpServerTool(Name = "suggest-strategy-for-sealed-hierarchy")]
    [Description("Suggests the [Arbitrary] + Strategy.OneOf pattern for sealed abstract class hierarchies. Use when the type is an abstract base of a sealed class hierarchy.")]
    public static string SuggestSealedHierarchyStrategy(
        [Description("The C# abstract base type name, e.g. 'Shape', 'Animal'")] string typeName)
    {
        return SuggestForSealedAbstractType(typeName.Trim());
    }

    internal static string SuggestForType(string typeName, bool hasArbitraryAttribute = false)
    {
        return hasArbitraryAttribute ? SuggestForArbitraryType(typeName) : SuggestForKnownType(typeName);
    }

    private static string SuggestForArbitraryType(string typeName)
    {
        return $$"""
            The type `{{typeName}}` is decorated with `[Arbitrary]`. Use `Strategy.For<{{typeName}}>()` — the source generator will emit the strategy automatically.

            **Primary recommendation:**
            ```csharp
            Strategy<{{typeName}}> strategy = Strategy.For<{{typeName}}>();
            ```

            **With property overrides:**
            ```csharp
            Strategy<{{typeName}}> strategy = Strategy.For<{{typeName}}>(cfg => cfg.Override(x => x.SomeProperty, Strategy.Integers<int>(min: 0, max: 100)));
            ```
            """;
    }

    private static string SuggestForKnownType(string typeName)
    {
        return typeName switch
        {
            "bool" =>
                "Use `Strategy.Booleans()` → `Strategy<bool>`.",

            "int" =>
                """
            Use `Strategy.Integers<int>()` for the full range, or `Strategy.Integers<int>(min, max)` for a bounded range.
            Works for all `IBinaryInteger<T>` types: `byte`, `short`, `long`, `uint`, `ulong`, `ushort`, `sbyte`.
            """,

            "long" or "short" or "byte" or "uint" or "ulong" or "ushort" or "sbyte" =>
                $"Use `Strategy.Integers<{typeName}>()` for full range, or `Strategy.Integers<{typeName}>(min, max)` for bounded.",

            "float" =>
                "Use `Strategy.Floats()` for the full range, or `Strategy.Floats(min, max)` for bounded. Includes NaN and ±Infinity.",

            "double" =>
                "Use `Strategy.Doubles()` for the full range, or `Strategy.Doubles(min, max)` for bounded. Includes NaN and ±Infinity.",

            "DateTimeOffset" =>
                """
            Use `Strategy.DateTimeOffsets()` for the full range, or `Strategy.DateTimeOffsets(min, max)` for bounded.

            For precision-sensitive roundtrip tests, chain the edge-case extensions from `Conjecture.Time`:
            ```csharp
            Strategy.DateTimeOffsets().WithPrecision(TimeSpan.FromMilliseconds(1))
            // → Strategy<DateTimeOffset> truncated to millisecond precision

            Strategy.DateTimeOffsets().WithStrippedOffset()
            // → Strategy<(DateTimeOffset Original, DateTimeOffset Stripped)>
            ```

            Add the NuGet package: `Conjecture.Time`
            """,

            "TimeSpan" =>
                "Use `Strategy.TimeSpans()` for the full range, or `Strategy.TimeSpans(min, max)` for bounded.",

            "DateOnly" =>
                """
            Use `Strategy.DateOnlyValues()` for the full range, or `Strategy.DateOnlyValues(min, max)` for bounded.

            For boundary-sensitive tests, chain the edge-case extensions from `Conjecture.Time`:
            ```csharp
            Strategy.DateOnlyValues().NearMonthBoundary()
            // → Strategy<DateOnly> biased to first/last day of each month

            Strategy.DateOnlyValues().NearLeapDay()
            // → Strategy<DateOnly> within ±1 day of Feb 29 in leap years
            ```

            Add the NuGet package: `Conjecture.Time`
            """,

            "TimeOnly" =>
                """
            Use `Strategy.TimeOnlyValues()` for the full range, or `Strategy.TimeOnlyValues(min, max)` for bounded.

            For boundary-sensitive tests, chain the edge-case extensions from `Conjecture.Time`:
            ```csharp
            Strategy.TimeOnlyValues().NearMidnight()
            // → Strategy<TimeOnly> within 30 s of midnight (wraps both ends)

            Strategy.TimeOnlyValues().NearNoon()
            // → Strategy<TimeOnly> within 30 s of 12:00:00

            Strategy.TimeOnlyValues().NearEndOfDay()
            // → Strategy<TimeOnly> within 30 s of 23:59:59
            ```

            Add the NuGet package: `Conjecture.Time`
            """,

            "DateTime" =>
                """
            Use `Strategy.DateTimes()` for the full range, or `Strategy.DateTimes(min, max)` for bounded.

            For kind-sensitive tests, chain the extension from `Conjecture.Time`:
            ```csharp
            Strategy.DateTimes().WithKinds()
            // → Strategy<(DateTime Value, DateTimeKind Kind)> covering Utc / Local / Unspecified
            ```

            Add the NuGet package: `Conjecture.Time`
            """,

            "TimeZoneInfo" =>
                """
            Use `Strategy.TimeZone(preferDst: true)` from `Conjecture.Time` to sample cross-platform time zones, biased toward zones with DST transitions:
            ```csharp
            Strategy.TimeZone(preferDst: true)
            // → Strategy<TimeZoneInfo> from the cross-platform DST-aware subset
            ```

            For raw IANA/Windows IDs, use `Strategy.IanaZoneIds()` or `Strategy.WindowsZoneIds()`.

            Add the NuGet package: `Conjecture.Time`
            """,

            "FakeTimeProvider" or "TimeProvider" =>
                """
            Use `Strategy.ClockWithAdvances(advanceCount, maxJump)` from `Conjecture.Time` to generate an adversarial clock paired with pre-drawn time advances:
            ```csharp
            Strategy.ClockWithAdvances(advanceCount: 5, maxJump: TimeSpan.FromMinutes(10))
            // → Strategy<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)>

            // Allow backward jumps to test clock-skew handling:
            Strategy.ClockWithAdvances(advanceCount: 3, maxJump: TimeSpan.FromSeconds(30), allowBackward: true)
            ```

            Add the NuGet package: `Conjecture.Time`
            """,

            "string" =>
                """
            Use `Strategy.Strings()` for random printable-ASCII strings (length 0–20 by default).

            Options:
            - `Strategy.Strings(minLength: 1, maxLength: 50)` — length bounds
            - `Strategy.Strings(minCodepoint: 65, maxCodepoint: 122)` — Unicode range
            - `Strategy.Strings(alphabet: "abcdef")` — fixed character set
            - `Strategy.Text()` — alias for Strings()
            """,

            "char" =>
                """
            There is no `Strategy.Chars()` built in. Use:
            ```csharp
            Strategy.Strings(minLength: 1, maxLength: 1).Select(s => s[0])
            ```
            """,

            "Guid" =>
                "Use `Strategy.Guids()` → `Strategy<Guid>`.",

            "Version" =>
                "Use `Strategy.Versions()` → `Strategy<Version>`.",

            "Half" =>
                """
            Use `Strategy.Halves()` for the full range, or `Strategy.Halves(min, max)` for bounded:
            ```csharp
            Strategy.Halves()
            // → Strategy<Half>

            Strategy.Halves(min, max)
            // → Strategy<Half> within [min, max]
            ```
            """,

            "Int128" =>
                """
            Use `Strategy.Integers<Int128>()` for the full range, or `Strategy.Integers<Int128>(min, max)` for bounded:
            ```csharp
            Strategy.Integers<Int128>()
            // → Strategy<Int128>

            Strategy.Integers<Int128>(min, max)
            // → Strategy<Int128> within [min, max]
            ```
            """,

            "UInt128" =>
                """
            Use `Strategy.Integers<UInt128>()` for the full range, or `Strategy.Integers<UInt128>(min, max)` for bounded:
            ```csharp
            Strategy.Integers<UInt128>()
            // → Strategy<UInt128>

            Strategy.Integers<UInt128>(min, max)
            // → Strategy<UInt128> within [min, max]
            ```
            """,

            "BigInteger" =>
                """
            Use `Strategy.Integers(min, max)` for a ranged `BigInteger` strategy. `BigInteger` is ranged-only — a parameterless form is not available because the range is unbounded.
            ```csharp
            Strategy.Integers(min: new BigInteger(0), max: new BigInteger(1_000_000))
            // → Strategy<BigInteger> within [min, max]
            ```
            """,

            "Rune" =>
                """
            Use `Strategy.Runes()` for the full Unicode scalar range, or `Strategy.Runes(min, max)` for bounded:
            ```csharp
            Strategy.Runes()
            // → Strategy<Rune>

            Strategy.Runes(min, max)
            // → Strategy<Rune> within [min, max]
            ```
            """,

            "byte[]" =>
                "Use `Strategy.Arrays(Strategy.Integers<byte>(), minSize, maxSize)` for a byte array → `Strategy<byte[]>`. Generic `Strategy.Arrays<T>(inner, minSize, maxSize)` works for any element type.",

            "IPAddress" =>
                "Use `Strategy.IPAddresses()` → `Strategy<IPAddress>` (V4/V6/Both via IPAddressKind).",

            "IPEndPoint" =>
                "Use `Strategy.IPEndPoints()` → `Strategy<IPEndPoint>` (composable address + port strategies).",

            "MailAddress" =>
                "Use `Strategy.EmailAddresses()` → `Strategy<MailAddress>` (or `Strategy.EmailAddressStrings()` for the string form).",

            "Uri" =>
                "Use `Strategy.Uris()` → `Strategy<Uri>` (UriKind, optional schemes).",

            "Regex" =>
                """
            Use `Strategy.Matching(pattern)` or `Strategy.NotMatching(pattern)` from the `Conjecture.Regex` package:
            ```csharp
            Strategy.Matching(@"^\d{3}-\d{4}$")
            // → Strategy<string> of strings matching the pattern

            Strategy.NotMatching(@"^\d{3}-\d{4}$")
            // → Strategy<string> of strings NOT matching the pattern
            ```

            For common patterns, use the `Generate` helpers contributed by `Conjecture.Regex`:
            ```csharp
            Strategy.Email()    // → Strategy<string> of valid email addresses
            Strategy.Url()      // → Strategy<string> of valid URLs
            ```

            Add the NuGet package: `Conjecture.Regex`
            """,

            "email" or "Email" =>
                """
            Use `Strategy.Email()` from the `Conjecture.Regex` package:
            ```csharp
            Strategy.Email()
            // → Strategy<string> of valid email address strings
            ```

            Add the NuGet package: `Conjecture.Regex`
            """,

            "ReDoS" or "redos" or "backtracking" or "Backtracking" or "catastrophic" or "Catastrophic" or "adversarial" or "Adversarial" =>
                """
            Use `Strategy.ReDoSHunter(pattern, maxMatchMs: 5)` from the `Conjecture.Regex` package to generate adversarial strings that trigger catastrophic backtracking:
            ```csharp
            Strategy.ReDoSHunter(@"(a+)+$", maxMatchMs: 5)
            // → Strategy<string> biased toward inputs that cause ReDoS timeouts
            ```

            Use with a timing assertion in your property:
            ```csharp
            var sw = Stopwatch.StartNew();
            Regex.IsMatch(input, pattern);
            Assert.True(sw.ElapsedMilliseconds < 100, $"Slow on: {input}");
            ```

            Add the NuGet package: `Conjecture.Regex`
            """,

            "decimal" =>
                """
            Use `Strategy.Decimal(min, max, scale)` for scaled decimal values:
            ```csharp
            Strategy.Decimal(min: 0m, max: 1000m, scale: 2)
            // → Strategy<decimal> with at most 2 decimal places
            ```

            For currency amounts, use the `Conjecture.Money` package:
            ```csharp
            using Conjecture.Money;

            Strategy.Amounts("USD")
            // → Strategy<decimal> of valid currency amounts in USD
            ```

            Add the NuGet package: `Conjecture.Money`
            """,

            "currency" or "currencies" or "currencyCode" or "ISO4217" or "iso4217" or "ISO 4217" =>
                """
            Use `Strategy.Iso4217Codes()` to sample currency codes, and `Strategy.Amounts(currencyCode)` for amounts:
            ```csharp
            using Conjecture.Money;

            Strategy.Iso4217Codes()
            // → Strategy<string> of ISO 4217 currency codes, e.g. "USD", "EUR", "GBP"

            Strategy.Amounts("USD")
            // → Strategy<decimal> of valid currency amounts for the given currency code
            ```

            For culture-dependent formatting and parsing, use `Strategy.CulturesWithCurrency()` or `Strategy.CulturesByCurrencyCode(string)`:
            ```csharp
            using Conjecture.Money;

            Strategy.CulturesByCurrencyCode("USD")
            // → Strategy<CultureInfo> samples cultures using USD; shrinks to en-US

            Strategy.Amounts("USD").Combine(Strategy.CulturesByCurrencyCode("USD"))
            // → Strategy<(decimal, CultureInfo)> for round-trip property tests
            ```

            Add the NuGet package: `Conjecture.Money`
            """,

            "MidpointRounding" or "rounding" =>
                """
            Use `Strategy.RoundingModes()` to sample `MidpointRounding` values:
            ```csharp
            using Conjecture.Money;

            Strategy.RoundingModes()
            // → Strategy<MidpointRounding>
            ```

            Add the NuGet package: `Conjecture.Money`
            """,

            "money" or "amount" or "price" =>
                """
            Use `Strategy.Amounts(currencyCode)` from the `Conjecture.Money` package:
            ```csharp
            using Conjecture.Money;

            Strategy.Amounts("USD")
            // → Strategy<decimal> of valid currency amounts in USD

            Strategy.Iso4217Codes()
            // → Strategy<string> of ISO 4217 currency codes if you need to vary the currency
            ```

            Add the NuGet package: `Conjecture.Money`
            """,

            "DistributedApplication" =>
                """
            This type is part of .NET Aspire. Use `AspireStateMachine<TState>` from `Conjecture.Aspire` to drive stateful end-to-end tests against a running `DistributedApplication`:

            ```csharp
            using Conjecture.Aspire;

            // Implement IStateMachine<TState, TCommand> and pass it to AspireStateMachine<TState>
            AspireStateMachine<MyState> machine = new(new MyStateMachine(), fixture.App);
            ```

            Add the NuGet package: `Conjecture.Aspire`
            """,

            "Interaction" =>
                """
            This type represents an Aspire interaction (HTTP or messaging). Use strategies from `Conjecture.Aspire`:

            ```csharp
            using Conjecture.Aspire;

            Strategy.HttpPost("/endpoint", Strategy.Strings())
            // → Strategy<Interaction> for HTTP POST interactions

            Strategy.PublishMessage("queue-name", Strategy.Strings())
            // → Strategy<Interaction> for message-publishing interactions
            ```

            Add the NuGet package: `Conjecture.Aspire`
            """,

            "IAspireAppFixture" =>
                """
            `IAspireAppFixture` is the contract for an Aspire test fixture in `Conjecture.Aspire`. It provides the running `DistributedApplication` and exposes `ResetAsync()` to restore state between property iterations.

            ```csharp
            using Conjecture.Aspire;

            public class MyAppFixture : IAspireAppFixture
            {
                public DistributedApplication App { get; }
                public Task ResetAsync() => /* reset state */;
            }
            ```

            Use `AspireStateMachine<TState>` to drive property tests:
            ```csharp
            AspireStateMachine<MyState> machine = new(new MyStateMachine(), fixture.App);
            ```

            Add the NuGet package: `Conjecture.Aspire`
            """,

            _ when typeName.StartsWith("AspireDbTarget<", StringComparison.Ordinal) =>
                $$"""
            This type is an Aspire + EF Core database target. Use `AspireDbTarget.CreateAsync<TContext>` with an explicit `contextFactory` to construct it, then assert invariants using `AspireEFCoreInvariants`:

            ```csharp
            using Conjecture.Aspire.EFCore;

            AspireDbTarget<{{InnerType(typeName)}}> dbTarget = await AspireDbTarget.CreateAsync<{{InnerType(typeName)}}>(
                contextFactory: sp => sp.GetRequiredService<{{InnerType(typeName)}}>());

            AspireEFCoreInvariants invariants = new(writer, dbTarget);
            await invariants.AssertNoPartialWritesOnErrorAsync(interaction, db => db.Set<Order>().CountAsync());
            await invariants.AssertIdempotentAsync(interaction, db => db.Set<Order>().CountAsync(), TimeSpan.FromSeconds(2));
            ```

            Register `AspireDbTargetRegistry` inside your `IAspireAppFixture.ResetAsync` override to reset all registered targets between property iterations.

            Add the NuGet package: `Conjecture.Aspire.EFCore`
            """,

            "AspireDbTargetRegistry" =>
                """
            `AspireDbTargetRegistry` tracks one or more `IDbTarget` instances for bulk reset. Wire it inside your `IAspireAppFixture.ResetAsync` override:

            ```csharp
            using Conjecture.Aspire.EFCore;

            public class MyAppFixture : IAspireAppFixture
            {
                private readonly AspireDbTargetRegistry _registry = new();

                public async Task ResetAsync()
                {
                    await _registry.ResetAllAsync();
                }
            }
            ```

            Register each `AspireDbTarget<TContext>` with `_registry.Register(dbTarget)` during fixture setup.

            Add the NuGet package: `Conjecture.Aspire.EFCore`
            """,

            "AspireEFCoreInvariants" =>
                """
            `AspireEFCoreInvariants` asserts EF Core-level invariants for Aspire-hosted services. Construct it with an `(IInteractionTarget writer, IDbTarget db)` pair:

            ```csharp
            using Conjecture.Aspire.EFCore;

            AspireEFCoreInvariants invariants = new(writer, dbTarget);

            // Available assertion methods:
            await invariants.AssertNoPartialWritesOnErrorAsync(interaction, db => db.Set<Order>().CountAsync());
            await invariants.AssertIdempotentAsync(interaction, db => db.Set<Order>().CountAsync(), TimeSpan.FromSeconds(2));
            ```

            Add the NuGet package: `Conjecture.Aspire.EFCore`
            """,

            "DbSnapshotInteraction" =>
                """
            `DbSnapshotInteraction` is an interaction step that snapshots the database state. Use the `AspireInteractionSequenceBuilder.DbSnapshot` builder method to add it to an interaction sequence:

            ```csharp
            using Conjecture.Aspire.EFCore;

            AspireInteractionSequenceBuilder.DbSnapshot(dbTarget)
            // → adds a DbSnapshotInteraction step to the sequence
            ```

            Add the NuGet package: `Conjecture.Aspire.EFCore`
            """,

            _ when typeName.StartsWith("WebApplicationFactory<", StringComparison.Ordinal)
                || typeName.StartsWith("AspNetCoreDbTarget<", StringComparison.Ordinal) =>
                """
            This type is an ASP.NET Core + EF Core composite test target. Use `AspNetCoreDbTarget<TContext>` and `HostHttpTarget` from `Conjecture.AspNetCore.EFCore`, constructed from the same `IHost`, and assert invariants using:

            ```csharp
            using Conjecture.AspNetCore;
            using Conjecture.AspNetCore.EFCore;

            IHost host = factory.Server.Host;
            HostHttpTarget httpTarget = new(host);
            AspNetCoreDbTarget<AppDbContext> dbTarget = new(host);

            await AspNetCoreEFCoreInvariants.AssertNoPartialWritesOnErrorAsync(httpTarget, dbTarget, request);
            await AspNetCoreEFCoreInvariants.AssertCascadeCorrectnessAsync(httpTarget, dbTarget, request);
            await AspNetCoreEFCoreInvariants.AssertIdempotentAsync(httpTarget, dbTarget, request);
            ```

            Wire the factory via `IClassFixture<WebApplicationFactory<TApp>>` in your test class.

            Add the NuGet package: `Conjecture.AspNetCore.EFCore`
            """,

            "DbContext" =>
                """
            This type is an EF Core `DbContext`. Use `Strategy.Entity<T>(context)` from `Conjecture.EFCore` to generate entities registered in the model:

            ```csharp
            using Conjecture.EFCore;

            Strategy.Entity<Order>(db)
            // → Strategy<Order> generating entities tracked by the DbContext
            ```

            Add the NuGet package: `Conjecture.EFCore`
            """,

            "EntitySet" =>
                """
            This type appears to be an EF Core entity. Use `Strategy.EntitySet<T>(context)` from `Conjecture.EFCore` to generate from the full set persisted in the database:

            ```csharp
            using Conjecture.EFCore;

            Strategy.EntitySet<Order>(db)
            // → Strategy<Order> sampling from existing rows in the DbSet<Order>
            ```

            For roundtrip testing, use `RoundtripAsserter`:
            ```csharp
            await RoundtripAsserter.AssertRoundtripsAsync(db, entity);
            ```

            Add the NuGet package: `Conjecture.EFCore`
            """,

            _ when typeName.StartsWith("DbSet<", StringComparison.Ordinal) =>
                """
            This type appears to be an EF Core entity. Use `Strategy.EntitySet<T>(context)` from `Conjecture.EFCore` to generate from the full set persisted in the database:

            ```csharp
            using Conjecture.EFCore;

            Strategy.EntitySet<Order>(db)
            // → Strategy<Order> sampling from existing rows in the DbSet<Order>
            ```

            For roundtrip testing, use `RoundtripAsserter`:
            ```csharp
            await RoundtripAsserter.AssertRoundtripsAsync(db, entity);
            ```

            Add the NuGet package: `Conjecture.EFCore`
            """,

            _ when typeName.StartsWith("List<", StringComparison.Ordinal) =>
                SuggestList(InnerType(typeName)),

            _ when typeName.StartsWith("IReadOnlyList<", StringComparison.Ordinal)
                || typeName.StartsWith("IList<", StringComparison.Ordinal)
                || typeName.StartsWith("IEnumerable<", StringComparison.Ordinal) =>
                SuggestList(InnerType(typeName)) + "\n\nNote: `Strategy.Lists` returns `Strategy<List<T>>`, which satisfies `IReadOnlyList<T>` and `IEnumerable<T>`.",

            _ when typeName.StartsWith("HashSet<", StringComparison.Ordinal)
                || typeName.StartsWith("ISet<", StringComparison.Ordinal)
                || typeName.StartsWith("IReadOnlySet<", StringComparison.Ordinal) =>
                SuggestSet(InnerType(typeName)),

            _ when typeName.StartsWith("Dictionary<", StringComparison.Ordinal)
                || typeName.StartsWith("IDictionary<", StringComparison.Ordinal)
                || typeName.StartsWith("IReadOnlyDictionary<", StringComparison.Ordinal) =>
                SuggestDictionary(),

            _ when typeName.EndsWith("?", StringComparison.Ordinal) =>
                SuggestNullable(typeName[..^1]),

            _ when typeName.StartsWith("Nullable<", StringComparison.Ordinal) =>
                SuggestNullable(typeName[9..^1]),

            _ when typeName.StartsWith("(", StringComparison.Ordinal) && typeName.EndsWith(")", StringComparison.Ordinal) =>
                """
            Use `Strategy.Tuples(strategy1, strategy2)` for 2-element tuples (up to 4 elements):
            ```csharp
            Strategy.Tuples(Strategy.Integers<int>(), Strategy.Strings())
            // → Strategy<(int, string)>

            Strategy.Tuples(Strategy.Integers<int>(), Strategy.Strings(), Strategy.Booleans())
            // → Strategy<(int, string, bool)>
            ```
            """,

            _ =>
                $$"""
            For custom type `{{typeName}}`, choose one of:

            **Option 1 – `Strategy.Compose` (recommended for simple types):**
            ```csharp
            Strategy.Compose(ctx => new {{typeName}}(
                ctx.Generate(Strategy.Integers<int>()),
                ctx.Generate(Strategy.Strings())
                // generate each field from ctx
            ))
            ```

            **Option 2 – Source generator (recommended for many tests):**
            Implement `IStrategyProvider<{{typeName}}>` and annotate with `[Arbitrary]`:
            ```csharp
            [Arbitrary]
            public partial class {{typeName}}Strategies : IStrategyProvider<{{typeName}}>
            {
                public static Strategy<{{typeName}}> GetStrategy() =>
                    Strategy.Compose(ctx => new {{typeName}}( /* ... */ ));
            }
            ```

            **Option 3 – If `{{typeName}}` is an enum:**
            ```csharp
            Strategy.Enums<{{typeName}}>()
            ```

            **Option 4 – If `{{typeName}}` has a known finite set of instances:**
            ```csharp
            Strategy.SampledFrom(new {{typeName}}[] { val1, val2, val3 })
            ```

            **Option 5 – If `{{typeName}}` can be derived from another strategy:**
            ```csharp
            Strategy.Strings().Select(s => new {{typeName}}(s))
            ```
            """
        };
    }

    private static string SuggestList(string inner) =>
        $"""
        Use `Strategy.Lists(innerStrategy)`:
        ```csharp
        Strategy.Lists({Inline(inner)})
        // → Strategy<List<{inner}>>

        // With size bounds:
        Strategy.Lists({Inline(inner)}, minSize: 1, maxSize: 10)
        ```
        """;

    private static string SuggestSet(string inner) =>
        $"""
        Use `Strategy.Sets(innerStrategy)`:
        ```csharp
        Strategy.Sets({Inline(inner)})
        // → Strategy<IReadOnlySet<{inner}>>

        Strategy.Sets({Inline(inner)}, minSize: 1, maxSize: 10)
        ```
        """;

    private static string SuggestDictionary() =>
        """
        Use `Strategy.Dictionaries(keyStrategy, valueStrategy)`:
        ```csharp
        Strategy.Dictionaries(Strategy.Strings(), Strategy.Integers<int>())
        // → Strategy<IReadOnlyDictionary<string, int>>

        // With size bounds:
        Strategy.Dictionaries(Strategy.Strings(), Strategy.Integers<int>(), minSize: 1, maxSize: 10)
        ```
        """;

    private static string SuggestNullable(string inner) =>
        $"""
        For `{inner}?`:
        - Value type: `Strategy.Nullable({Inline(inner)})` → `Strategy<{inner}?>`
        - Reference type: `{Inline(inner)}.OrNull()` → `Strategy<{inner}?>`
        """;

    internal static string SuggestForSealedAbstractType(string typeName) =>
        $$"""
        For sealed abstract type `{{typeName}}`, annotate the abstract base with `[Arbitrary]` and each concrete subtype with `[Arbitrary]`.
        The source generator will emit a `Strategy.OneOf(...)` strategy automatically — no manual wiring required.

        ```csharp
        [Arbitrary]
        public abstract partial class {{typeName}} { }

        [Arbitrary]
        public partial class ConcreteSubtype1 : {{typeName}} { }

        [Arbitrary]
        public partial class ConcreteSubtype2 : {{typeName}} { }

        // → {{typeName}}Arbitrary emitted automatically
        ```

        **Note:** Every concrete subtype of a sealed abstract base must be annotated with `[Arbitrary]`, or the analyzer will raise CON205.
        """;

    private static string Inline(string typeName) => typeName switch
    {
        "int" => "Strategy.Integers<int>()",
        "long" => "Strategy.Integers<long>()",
        "string" => "Strategy.Strings()",
        "bool" => "Strategy.Booleans()",
        "double" => "Strategy.Doubles()",
        "float" => "Strategy.Floats()",
        _ => $"/* strategy for {typeName} */"
    };

    private static string InnerType(string typeName)
    {
        int start = typeName.IndexOf('<') + 1;
        return typeName[start..^1];
    }
}