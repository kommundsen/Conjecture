# Observability

Conjecture emits structured log events during every test run — assumption rejection rates, shrink progress, timing, and failures. All four adapters auto-wire logging to their native test output mechanism with zero user configuration.

## Auto-Wired Output

Each adapter routes Conjecture logs to the test framework's native output automatically:

# [xUnit v2](#tab/xunit-v2)

Logs appear in the `ITestOutputHelper` output for any test class that declares it. No configuration required.

```csharp
using Conjecture.Xunit;
using Xunit;
using Xunit.Abstractions;

public class MyTests(ITestOutputHelper output)
{
    [Property]
    public void Reverse_TwiceIsIdentity(List<int> xs)
    {
        // Conjecture logs automatically appear in `output`
        Assert.Equal(xs, xs.AsEnumerable().Reverse().Reverse().ToList());
    }
}
```

Test classes without `ITestOutputHelper` use `NullLogger.Instance` — no output, no exceptions.

# [xUnit v3](#tab/xunit-v3)

Logs appear in the xUnit v3 test output automatically. No configuration required.

```csharp
using Conjecture.Xunit.V3;
using Xunit;

public class MyTests
{
    [Property]
    public void Reverse_TwiceIsIdentity(List<int> xs)
    {
        // Conjecture logs automatically appear in test output
        Assert.Equal(xs, xs.AsEnumerable().Reverse().Reverse().ToList());
    }
}
```

# [NUnit](#tab/nunit)

Logs appear in `TestContext.Out` automatically. No configuration required.

```csharp
using Conjecture.NUnit;
using NUnit.Framework;

[TestFixture]
public class MyTests
{
    [Property]
    public void Reverse_TwiceIsIdentity(List<int> xs)
    {
        // Conjecture logs automatically appear in TestContext.Out
        Assert.That(xs.AsEnumerable().Reverse().Reverse(), Is.EqualTo(xs));
    }
}
```

# [MSTest](#tab/mstest)

Logs appear via `Console.WriteLine`, which MSTest captures by default (`CaptureTraceOutput=true`). No configuration required.

```csharp
using Conjecture.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class MyTests
{
    [Property]
    public void Reverse_TwiceIsIdentity(List<int> xs)
    {
        // Conjecture logs automatically appear in test output
        CollectionAssert.AreEqual(xs, xs.AsEnumerable().Reverse().Reverse().ToList());
    }
}
```

---

## Typical Output

A passing run looks like this:

```
[info] Generation complete: valid=100, unsatisfied=0, elapsed=42ms
```

A failing run includes shrink events:

```
[info] Generation complete: valid=23, unsatisfied=0, elapsed=18ms
[info] Shrinking started: 47 nodes
[info] Shrinking complete: 12 nodes, 31 steps, elapsed=8ms
[error] Property test failed after 23 example(s), seed=0xDEADBEEF
```

A targeted run adds targeting events:

```
[info] Generation complete: valid=50, unsatisfied=0, elapsed=22ms
[info] Targeting started: labels=list_length
[info] Targeting complete: labels=list_length, best=87.0
```

## Custom Structured Logging

Supply an `ILogger` via `ConjectureSettings.Logger` to integrate Conjecture events into your application's logging pipeline:

```csharp
using Microsoft.Extensions.Logging;

ILoggerFactory factory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

ILogger logger = factory.CreateLogger("Conjecture");

ConjectureSettings settings = new() { Logger = logger, MaxExamples = 200 };
TestRunResult result = await TestRunner.Run(settings, data =>
{
    int x = data.NextInteger(0, 100);
    Assert.True(x >= 0);
});
```

To suppress all output programmatically:

```csharp
using Microsoft.Extensions.Logging.Abstractions;

ConjectureSettings settings = new() { Logger = NullLogger.Instance };
```

## Log Event Catalog

All events use the `Conjecture.Core.Internal.Log` source-generated class. EventIds are stable across versions.

| EventId | Level | Message Template |
|---|---|---|
| 1 | Information | `Generation complete: valid={Valid}, unsatisfied={Unsatisfied}, elapsed={DurationMs}ms` |
| 2 | Information | `Shrinking started: {NodeCount} nodes` |
| 3 | Information | `Shrinking complete: {NodeCount} nodes, {ShrinkCount} steps, elapsed={DurationMs}ms` |
| 4 | Information | `Targeting started: labels={Labels}` |
| 5 | Information | `Targeting complete: labels={Labels}, best={BestScores}` |
| 6 | Information | `Replaying {BufferCount} stored example(s) from database` |
| 7 | Warning | `High assumption rejection: {Unsatisfied} unsatisfied vs {Valid} valid (limit={Limit})` |
| 8 | Warning | `Database error: {ErrorMessage}` |
| 9 | Error | `Property test failed after {ExampleCount} example(s), seed={Seed}` |
| 10 | Debug | `Shrink pass {PassName}: progress={MadeProgress}` |
| 11 | Debug | `Database saved: testId={TestIdHash}` |
| 12 | Debug | `Targeting step: label={Label} improved to score={NewScore}` |

Debug events (10–12) are suppressed by default. Enable them by setting `LogLevel.Debug` on your logger.

## Performance

- **`[LoggerMessage]` source generation** — all log methods are generated at compile time. There is no boxing, no runtime template parsing, and no allocation when a log level is disabled.
- **`IsEnabled` guards** — debug-level events (shrink pass progress, targeting steps) are unconditionally guarded: the method body is skipped entirely when `Debug` is disabled, with zero overhead.
- **No inner-loop instrumentation** — `ConjectureData` draw methods and per-shrink-attempt loops are never instrumented. Only aggregate phase boundaries (generation complete, shrink complete, etc.) emit events.
- **`NullLogger.Instance`** — `ConjectureSettings.Logger` defaults to `NullLogger.Instance` when no adapter is auto-wiring. All log calls become no-ops with no allocation.
