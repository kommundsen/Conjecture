# How to audit a regex for catastrophic backtracking

Use `Generate.ReDoSHunter` to generate adversarial strings that expose
[catastrophic backtracking](../explanation/regex-catastrophic-backtracking.md) in a regex — then
shrink the failure to the smallest possible pathological input.

## Install

```bash
dotnet add package Conjecture.Regex
```

## Write the property

Wrap `Generate.ReDoSHunter` in a property that measures how long the regex takes on each
generated string:

```csharp
using System.Diagnostics;
using System.Text.RegularExpressions;
using Conjecture.Core;
using Conjecture.Regex;

[Property]
public bool UserInputPattern_DoesNotBacktrackCatastrophically()
{
    string input = DataGen.SampleOne(Generate.ReDoSHunter(@"(a+)+$", maxMatchMs: 50));

    Stopwatch sw = Stopwatch.StartNew();
    Regex.IsMatch(input, @"(a+)+$");
    sw.Stop();

    return sw.ElapsedMilliseconds < 50;
}
```

Conjecture generates strings adversarially biased toward inputs that exhaust the backtracking
engine. When the property fails, the shrunk counterexample is the shortest string that still
causes the slowdown.

## Choose a `maxMatchMs` budget

`maxMatchMs` controls the internal engine-level timeout used to confirm whether a candidate is
genuinely pathological:

| Scenario | Recommended value |
|---|---|
| Tight CI budget (fast feedback) | `5`–`10` ms |
| Standard CI | `25`–`50` ms (default: `5`) |
| Noisy environment / low-frequency audit | `100`–`200` ms |

The value should be lower than the threshold you assert in your property — the strategy uses it
internally to identify slow candidates, not to gate the property itself.

> [!TIP]
> Start with `maxMatchMs: 5`. If you see false positives on a loaded CI machine, increase to
> `25` or `50`. The built-in confirmation logic (3 trials, ≥ 2 hits) already guards against
> spurious single-run timeouts.

## Interpret a failure

When the property fails, the counterexample output will show something like:

```
Falsified after 12 tests
Counterexample: "aaaaaaaaaa\0"
Shrunk from: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\0"
```

The `\0` suffix is appended by the strategy to force the trailing `$` anchor to fail — this is
what triggers catastrophic backtracking. The shrunk string is the minimal input where the pattern
exceeds your timing budget.

To reproduce the failure manually:

```csharp
Regex r = new(@"(a+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(50));
r.IsMatch("aaaaaaaaaa\0"); // throws RegexMatchTimeoutException
```

## Verify the pattern is genuinely vulnerable

Not every slow regex is catastrophically vulnerable. Check the shrunk counterexample:

1. **Length** — a genuinely vulnerable pattern slows down super-linearly. If the failure
   disappears when you double the string length (by repeating the example), it is likely a
   coincidence.
2. **Structure** — the shrunk string will usually look like `aaa...\0` or similar, tracing the
   ambiguous repetition structure of the pattern.
3. **Fix** — switch to `RegexOptions.NonBacktracking` to confirm the pattern is safe there:

```csharp
// Safe: NonBacktracking engine cannot exhibit catastrophic backtracking
Regex safe = new(@"(a+)+$", RegexOptions.NonBacktracking);
safe.IsMatch("aaaaaaaaaa\0"); // returns quickly
```

## Audit a regex used in production

Pass a compiled `Regex` instance to share the same options:

```csharp
[GeneratedRegex(@"^([a-zA-Z0-9_]+\.)*[a-zA-Z0-9_]+$")]
private static partial Regex HostnamePattern();

[Property]
public bool HostnamePattern_IsNotVulnerableToReDoS()
{
    string input = DataGen.SampleOne(
        Generate.ReDoSHunter(HostnamePattern(), maxMatchMs: 25));

    Stopwatch sw = Stopwatch.StartNew();
    HostnamePattern().IsMatch(input);
    sw.Stop();

    return sw.ElapsedMilliseconds < 25;
}
```

## Handle `RegexOptions.NonBacktracking`

When the regex uses `RegexOptions.NonBacktracking`, `Generate.ReDoSHunter` automatically falls
back to `Generate.Matching` with the diagnostic label `"redos:non-backtracking"`. The property
will still run but will never find a catastrophic input — which is the correct result, since the
NFA engine cannot exhibit catastrophic backtracking by design:

```csharp
Regex safe = new(@"(a+)+$", RegexOptions.NonBacktracking);
Strategy<string> strategy = Generate.ReDoSHunter(safe, maxMatchMs: 5);

Console.WriteLine(strategy.Label); // "redos:non-backtracking"
```

## See also

- [Reference: `Generate.ReDoSHunter`](../reference/regex-strategies.md#generateredoshunter)
- [Explanation: Why nested quantifiers cause catastrophic backtracking](../explanation/regex-catastrophic-backtracking.md)
- [How to test a regex validator](test-regex-validator.md)
