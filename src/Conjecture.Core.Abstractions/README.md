# Conjecture.Core.Abstractions

Extension-author contracts for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Reference this package when building a custom strategy formatter, source generator, or low-level extension. End-user test code should reference [`Conjecture.Core`](https://www.nuget.org/packages/Conjecture.Core) instead.

## Who is this for?

- **Formatter authors** — implement `IStrategyFormatter<T>` to control how a type's shrunk value is displayed in failure output.
- **Source-generator authors** — reference `IStrategyProvider<T>` without pulling in the full Conjecture engine.

All types in this package are marked `[EditorBrowsable(Never)]` and live under the `Conjecture.Abstractions` namespace to keep them out of end-user IntelliSense.

## Install

```
dotnet add package Conjecture.Core.Abstractions
```

## Types

| Type | Role |
|---|---|
| `IStrategyFormatter<T>` | Formats a value of type `T` as a string for counterexample output. Register via `FormatterRegistry` in `Conjecture.Core`. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
