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

## Analyzers and Source Generator

Roslyn analyzers and the `[Arbitrary]` source generator are bundled into `Conjecture.Core` and activate automatically — no additional packages needed.

## Namespace

All core types live in the `Conjecture.Core` namespace. Framework adapters use their own namespace:

# [xUnit v2](#tab/xunit-v2)

```csharp
using Conjecture.Core;   // Generate, Strategy<T>, Assume, ConjectureSettings, ...
using Conjecture.Xunit;  // [Property], [Example], [From<T>], [FromFactory], ...
```

# [xUnit v3](#tab/xunit-v3)

```csharp
using Conjecture.Core;       // Generate, Strategy<T>, Assume, ConjectureSettings, ...
using Conjecture.Xunit.V3;  // [Property], [Example], [From<T>], [FromFactory], ...
```

# [NUnit](#tab/nunit)

```csharp
using Conjecture.Core;   // Generate, Strategy<T>, Assume, ConjectureSettings, ...
using Conjecture.NUnit;  // [Property], [Example], [From<T>], [FromFactory], ...
```

# [MSTest](#tab/mstest)

```csharp
using Conjecture.Core;    // Generate, Strategy<T>, Assume, ConjectureSettings, ...
using Conjecture.MSTest;  // [Property], [Example], [From<T>], [FromFactory], ...
```

***
