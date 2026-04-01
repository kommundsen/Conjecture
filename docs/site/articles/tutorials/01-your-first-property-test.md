# Tutorial 1: Your First Property Test

This tutorial walks you through writing and running your first property-based test with Conjecture.NET.

## Prerequisites

- .NET 10 SDK
- A test project with `Conjecture.Xunit` installed (see [Installation](../installation.md))

## What We're Testing

Suppose you have a `StringUtils` class:

```csharp
public static class StringUtils
{
    public static string Reverse(string input)
    {
        var chars = input.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }
}
```

A traditional unit test might check a few specific cases. A property test describes a rule that must hold for *all* strings.

## Writing the Property

```csharp
using Conjecture.Xunit;

public class StringUtilsTests
{
    [Property]
    public bool Reverse_twice_returns_original(string input)
    {
        return StringUtils.Reverse(StringUtils.Reverse(input)) == input;
    }
}
```

That's it. The `[Property]` attribute tells Conjecture to:

1. Generate 100 random `string` values
2. Pass each to your method
3. Assert the return value is `true`
4. If any fails, shrink to the smallest counterexample

## Running It

```bash
dotnet test
```

You'll see output like:

```
Passed StringUtilsTests.Reverse_twice_returns_original [100 examples]
```

## Properties That Use Assertions

Return `void` and use your framework's assertions instead of returning `bool`:

```csharp
[Property]
public void Reverse_preserves_length(string input)
{
    Assert.Equal(input.Length, StringUtils.Reverse(input).Length);
}
```

## A Failing Property

Let's write a property that exposes a bug. Suppose someone wrote a broken `Capitalize` method:

```csharp
public static string Capitalize(string input) =>
    input.Length > 0 ? char.ToUpper(input[0]) + input[1..] : input;
```

Test it:

```csharp
[Property]
public bool Capitalize_starts_with_uppercase(string input)
{
    var result = StringUtils.Capitalize(input);
    return result.Length == 0 || char.IsUpper(result[0]);
}
```

Conjecture will find that inputs like `"1abc"` (starting with a digit) violate the property, and shrink to the minimal counterexample — likely a single-character non-letter string.

## Filtering with `Assume`

If you want to test only non-empty strings:

```csharp
[Property]
public bool Capitalize_non_empty(string input)
{
    Assume.That(input.Length > 0);
    return StringUtils.Capitalize(input).Length > 0;
}
```

`Assume.That` skips the current example if the condition is false. Use it sparingly — if too many examples are filtered, Conjecture raises `ConjectureException`.

## Key Takeaways

- `[Property]` replaces `[Fact]` — Conjecture generates inputs automatically
- Return `bool` for simple assertions, `void` for framework assertions
- Shrinking is automatic — you get the smallest failing input for free
- `Assume.That(condition)` filters unwanted inputs

## Next

[Tutorial 2: Strategies and Composition](02-strategies-and-composition.md) — learn how to control what gets generated.
