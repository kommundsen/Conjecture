# How to install Conjecture

Conjecture.NET requires **.NET 10** or later.

## Install the adapter package

Install the package for your test framework. Each adapter transitively references `Conjecture.Core` — you only need one package.

| Test framework | Package |
|---|---|
| xUnit v2 | `Conjecture.Xunit` |
| xUnit v3 | `Conjecture.Xunit.V3` |
| NUnit 4 | `Conjecture.NUnit` |
| MSTest | `Conjecture.MSTest` |
| MTP (no framework) | `Conjecture.TestingPlatform` |

# [xUnit v2](#tab/xunit-v2)

```bash
dotnet add package Conjecture.Xunit
```

# [xUnit v3](#tab/xunit-v3)

```bash
dotnet add package Conjecture.Xunit.V3
```

# [NUnit](#tab/nunit)

```bash
dotnet add package Conjecture.NUnit
```

# [MSTest](#tab/mstest)

```bash
dotnet add package Conjecture.MSTest
```

# [MTP](#tab/mtp)

```bash
dotnet add package Conjecture.TestingPlatform
```

Also set `OutputType=Exe` in the `.csproj`. See [How to use the MTP adapter](use-mtp-adapter.md) for full setup.

***

## Add using directives

All core types live in `Conjecture.Core`. The `[Property]` attribute and parameter resolution attributes come from the adapter namespace:

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

# [MTP](#tab/mtp)

```csharp
using Conjecture.Core;              // Generate, Strategy<T>, Assume, ...
using Conjecture.TestingPlatform;   // [Property], [Example], [From<T>], [FromFactory], ...
```

***

## Analyzers and source generator

Roslyn analyzers and the `[Arbitrary]` source generator are bundled into `Conjecture.Core` and activate automatically — no additional packages needed.

## Next

[Quick Start](../quick-start.md) — write and run your first property test in 5 minutes.
