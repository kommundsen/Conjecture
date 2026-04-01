# Installation

Conjecture.NET requires **.NET 10** or later.

## Packages

Install the adapter package for your test framework. Each adapter transitively references `Conjecture.Core`, so you only need one package:

| Test Framework | Package | Command |
|---|---|---|
| xUnit v2 | `Conjecture.Xunit` | `dotnet add package Conjecture.Xunit` |
| xUnit v3 | `Conjecture.Xunit.V3` | `dotnet add package Conjecture.Xunit.V3` |
| NUnit 4 | `Conjecture.NUnit` | `dotnet add package Conjecture.NUnit` |
| MSTest | `Conjecture.MSTest` | `dotnet add package Conjecture.MSTest` |

## Optional Packages

| Package | Purpose |
|---|---|
| `Conjecture.Generators` | Source generator — derive strategies for your types with `[Arbitrary]` |
| `Conjecture.Analyzers` | Roslyn analyzers — catch common property-test mistakes at compile time |

Install them alongside your adapter:

```bash
dotnet add package Conjecture.Generators
dotnet add package Conjecture.Analyzers
```

## Namespace

All core types live in the `Conjecture.Core` namespace. Framework adapters use their own namespace (e.g., `Conjecture.Xunit`).

```csharp
using Conjecture.Core;   // Generate, Strategy<T>, Assume, ConjectureSettings, ...
using Conjecture.Xunit;  // [Property], [Example], [From<T>], [FromFactory], ...
```
