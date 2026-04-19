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
        "Suggests the Conjecture Generate.* strategy to use for a given C# type or description. " +
        "Handles primitives (int, bool, string, float, double, byte[]), date/time types " +
        "(DateTimeOffset, TimeSpan, DateOnly, TimeOnly), collections " +
        "(List<T>, IReadOnlySet<T>, IReadOnlyDictionary<K,V>), nullable types, value " +
        "tuples, enums, and custom types. Also handles regex-constrained strings via the " +
        "Conjecture.Regex package: pass 'Regex' for pattern-matching strategies, or a " +
        "description keyword such as 'email' for curated RegexGenerate helpers. " +
        "For sealed abstract class hierarchies, use the suggest-strategy-for-sealed-hierarchy tool instead.")]
    public static string SuggestStrategy(
        [Description("The C# type name, e.g. 'int', 'string', 'List<int>', 'MyEnum', 'MyRecord'")] string typeName)
    {
        return SuggestForType(typeName.Trim());
    }

    [McpServerTool(Name = "suggest-strategy-for-sealed-hierarchy")]
    [Description("Suggests the [Arbitrary] + Generate.OneOf pattern for sealed abstract class hierarchies. Use when the type is an abstract base of a sealed class hierarchy.")]
    public static string SuggestSealedHierarchyStrategy(
        [Description("The C# abstract base type name, e.g. 'Shape', 'Animal'")] string typeName)
    {
        return SuggestForSealedAbstractType(typeName.Trim());
    }

    internal static string SuggestForType(string typeName) => typeName switch
    {
        "bool" =>
            "Use `Generate.Booleans()` → `Strategy<bool>`.",

        "int" =>
            """
            Use `Generate.Integers<int>()` for the full range, or `Generate.Integers<int>(min, max)` for a bounded range.
            Works for all `IBinaryInteger<T>` types: `byte`, `short`, `long`, `uint`, `ulong`, `ushort`, `sbyte`.
            """,

        "long" or "short" or "byte" or "uint" or "ulong" or "ushort" or "sbyte" =>
            $"Use `Generate.Integers<{typeName}>()` for full range, or `Generate.Integers<{typeName}>(min, max)` for bounded.",

        "float" =>
            "Use `Generate.Floats()` for the full range, or `Generate.Floats(min, max)` for bounded. Includes NaN and ±Infinity.",

        "double" =>
            "Use `Generate.Doubles()` for the full range, or `Generate.Doubles(min, max)` for bounded. Includes NaN and ±Infinity.",

        "DateTimeOffset" =>
            "Use `Generate.DateTimeOffsets()` for the full range, or `Generate.DateTimeOffsets(min, max)` for bounded.",

        "TimeSpan" =>
            "Use `Generate.TimeSpans()` for the full range, or `Generate.TimeSpans(min, max)` for bounded.",

        "DateOnly" =>
            "Use `Generate.DateOnlyValues()` for the full range, or `Generate.DateOnlyValues(min, max)` for bounded.",

        "TimeOnly" =>
            "Use `Generate.TimeOnlyValues()` for the full range, or `Generate.TimeOnlyValues(min, max)` for bounded.",

        "string" =>
            """
            Use `Generate.Strings()` for random printable-ASCII strings (length 0–20 by default).

            Options:
            - `Generate.Strings(minLength: 1, maxLength: 50)` — length bounds
            - `Generate.Strings(minCodepoint: 65, maxCodepoint: 122)` — Unicode range
            - `Generate.Strings(alphabet: "abcdef")` — fixed character set
            - `Generate.Text()` — alias for Strings()
            """,

        "char" =>
            """
            There is no `Generate.Chars()` built in. Use:
            ```csharp
            Generate.Strings(minLength: 1, maxLength: 1).Select(s => s[0])
            ```
            """,

        "byte[]" =>
            "Use `Generate.Bytes(size)` for a fixed-length byte array → `Strategy<byte[]>`.",

        "Regex" =>
            """
            Use `Generate.Matching(pattern)` or `Generate.NotMatching(pattern)` from the `Conjecture.Regex` package:
            ```csharp
            Generate.Matching(@"^\d{3}-\d{4}$")
            // → Strategy<string> of strings matching the pattern

            Generate.NotMatching(@"^\d{3}-\d{4}$")
            // → Strategy<string> of strings NOT matching the pattern
            ```

            For common patterns, use the `Generate` helpers contributed by `Conjecture.Regex`:
            ```csharp
            Generate.Email()    // → Strategy<string> of valid email addresses
            Generate.Url()      // → Strategy<string> of valid URLs
            ```

            Add the NuGet package: `Conjecture.Regex`
            """,

        "email" or "Email" =>
            """
            Use `Generate.Email()` from the `Conjecture.Regex` package:
            ```csharp
            Generate.Email()
            // → Strategy<string> of valid email address strings
            ```

            Add the NuGet package: `Conjecture.Regex`
            """,

        _ when typeName.StartsWith("List<", StringComparison.Ordinal) =>
            SuggestList(InnerType(typeName)),

        _ when typeName.StartsWith("IReadOnlyList<", StringComparison.Ordinal)
            || typeName.StartsWith("IList<", StringComparison.Ordinal)
            || typeName.StartsWith("IEnumerable<", StringComparison.Ordinal) =>
            SuggestList(InnerType(typeName)) + "\n\nNote: `Generate.Lists` returns `Strategy<List<T>>`, which satisfies `IReadOnlyList<T>` and `IEnumerable<T>`.",

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
            Use `Generate.Tuples(strategy1, strategy2)` for 2-element tuples (up to 4 elements):
            ```csharp
            Generate.Tuples(Generate.Integers<int>(), Generate.Strings())
            // → Strategy<(int, string)>

            Generate.Tuples(Generate.Integers<int>(), Generate.Strings(), Generate.Booleans())
            // → Strategy<(int, string, bool)>
            ```
            """,

        _ =>
            $$"""
            For custom type `{{typeName}}`, choose one of:

            **Option 1 – `Generate.Compose` (recommended for simple types):**
            ```csharp
            Generate.Compose(ctx => new {{typeName}}(
                ctx.Generate(Generate.Integers<int>()),
                ctx.Generate(Generate.Strings())
                // draw each field from ctx
            ))
            ```

            **Option 2 – Source generator (recommended for many tests):**
            Implement `IStrategyProvider<{{typeName}}>` and annotate with `[Arbitrary]`:
            ```csharp
            [Arbitrary]
            public partial class {{typeName}}Strategies : IStrategyProvider<{{typeName}}>
            {
                public static Strategy<{{typeName}}> GetStrategy() =>
                    Generate.Compose(ctx => new {{typeName}}( /* ... */ ));
            }
            ```

            **Option 3 – If `{{typeName}}` is an enum:**
            ```csharp
            Generate.Enums<{{typeName}}>()
            ```

            **Option 4 – If `{{typeName}}` has a known finite set of instances:**
            ```csharp
            Generate.SampledFrom(new {{typeName}}[] { val1, val2, val3 })
            ```

            **Option 5 – If `{{typeName}}` can be derived from another strategy:**
            ```csharp
            Generate.Strings().Select(s => new {{typeName}}(s))
            ```
            """
    };

    private static string SuggestList(string inner) =>
        $"""
        Use `Generate.Lists(innerStrategy)`:
        ```csharp
        Generate.Lists({Inline(inner)})
        // → Strategy<List<{inner}>>

        // With size bounds:
        Generate.Lists({Inline(inner)}, minSize: 1, maxSize: 10)
        ```
        """;

    private static string SuggestSet(string inner) =>
        $"""
        Use `Generate.Sets(innerStrategy)`:
        ```csharp
        Generate.Sets({Inline(inner)})
        // → Strategy<IReadOnlySet<{inner}>>

        Generate.Sets({Inline(inner)}, minSize: 1, maxSize: 10)
        ```
        """;

    private static string SuggestDictionary() =>
        """
        Use `Generate.Dictionaries(keyStrategy, valueStrategy)`:
        ```csharp
        Generate.Dictionaries(Generate.Strings(), Generate.Integers<int>())
        // → Strategy<IReadOnlyDictionary<string, int>>

        // With size bounds:
        Generate.Dictionaries(Generate.Strings(), Generate.Integers<int>(), minSize: 1, maxSize: 10)
        ```
        """;

    private static string SuggestNullable(string inner) =>
        $"""
        For `{inner}?`:
        - Value type: `Generate.Nullable({Inline(inner)})` → `Strategy<{inner}?>`
        - Reference type: `{Inline(inner)}.OrNull()` → `Strategy<{inner}?>`
        """;

    internal static string SuggestForSealedAbstractType(string typeName) =>
        $$"""
        For sealed abstract type `{{typeName}}`, annotate the abstract base with `[Arbitrary]` and each concrete subtype with `[Arbitrary]`.
        The source generator will emit a `Generate.OneOf(...)` strategy automatically — no manual wiring required.

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
        "int" => "Generate.Integers<int>()",
        "long" => "Generate.Integers<long>()",
        "string" => "Generate.Strings()",
        "bool" => "Generate.Booleans()",
        "double" => "Generate.Doubles()",
        "float" => "Generate.Floats()",
        _ => $"/* strategy for {typeName} */"
    };

    private static string InnerType(string typeName)
    {
        var start = typeName.IndexOf('<') + 1;
        return typeName[start..^1];
    }
}