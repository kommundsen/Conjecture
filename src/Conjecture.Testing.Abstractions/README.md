# Conjecture.Testing.Abstractions

Test-framework adapter contracts for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Reference this package when building a custom test-framework adapter (e.g. `Conjecture.SomeUnit`). End-user test code should reference an existing adapter — [`Conjecture.Xunit`](https://www.nuget.org/packages/Conjecture.Xunit), [`Conjecture.NUnit`](https://www.nuget.org/packages/Conjecture.NUnit), [`Conjecture.MSTest`](https://www.nuget.org/packages/Conjecture.MSTest), etc. — instead.

## Who is this for?

Authors implementing a `[Property]`-style attribute for a test framework not already supported. Depend only on this package; you do **not** need `InternalsVisibleTo` grants from `Conjecture.Core`.

## Install

```
dotnet add package Conjecture.Testing.Abstractions
```

## Types

| Type | Role |
|---|---|
| `IPropertyTest` | Contract for a `[Property]` attribute. Exposes `MaxExamples`, `Seed`, `Database`, `DeadlineMs`, and other run controls. |
| `IReproductionExport` | Implemented by attributes that write a reproduction seed back to the source file after a failure. |
| `TestOutputLogger` | `ILogger` adapter that writes to any `Action<string>` (e.g. `testOutputHelper.WriteLine`). Use `TestOutputLogger.FromWriteLine(action)`. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
