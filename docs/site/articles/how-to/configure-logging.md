# How to configure logging

Conjecture emits structured log events covering assumption rejection rates, shrink progress, timing, and failures. All four adapters auto-wire logging to their native test output with zero configuration.

## Auto-wired output

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
        CollectionAssert.AreEqual(xs, xs.AsEnumerable().Reverse().Reverse().ToList());
    }
}
```

***

## Typical output

A passing run:

```text
[info] Generation complete: valid=100, unsatisfied=0, elapsed=42ms
```

A failing run includes shrink events:

```text
[info] Generation complete: valid=23, unsatisfied=0, elapsed=18ms
[info] Shrinking started: 47 nodes
[info] Shrinking complete: 12 nodes, 31 steps, elapsed=8ms
[error] Property test failed after 23 example(s), seed=0xDEADBEEF
```

A targeted run adds targeting events:

```text
[info] Generation complete: valid=50, unsatisfied=0, elapsed=22ms
[info] Targeting started: labels=list_length
[info] Targeting complete: labels=list_length, best=87.0
```

## Supply a custom logger

Pass an `ILogger` via `ConjectureSettings.Logger` to integrate Conjecture events into your application's logging pipeline:

```csharp
using Microsoft.Extensions.Logging;

ILoggerFactory factory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

ILogger logger = factory.CreateLogger("Conjecture");

ConjectureSettings settings = new() { Logger = logger, MaxExamples = 200 };
```

To suppress all output:

```csharp
using Microsoft.Extensions.Logging.Abstractions;

ConjectureSettings settings = new() { Logger = NullLogger.Instance };
```

## Log event catalog

All events are generated at compile time via `[LoggerMessage]` — no boxing, no runtime template parsing.

| EventId | Level | Message template |
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

> [!NOTE]
> `IsEnabled` guards are unconditional: when debug logging is disabled, the method body is skipped entirely with zero overhead.
