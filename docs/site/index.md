---
_layout: landing
---

# Conjecture

**Property-based testing for .NET** — a ground-up port of Python's [Hypothesis](https://hypothesis.readthedocs.io/) to C#.

Write tests that describe *what* your code should do, and let Conjecture generate hundreds of random inputs to find the edge cases you'd never think of. When it finds a failure, it automatically **shrinks** the input to the smallest possible counterexample.

## Quick Example

```csharp
using Conjecture.Xunit;

public class SortTests
{
    [Property]
    public bool Sorting_is_idempotent(List<int> items)
    {
        var sorted = items.OrderBy(x => x).ToList();
        var sortedTwice = sorted.OrderBy(x => x).ToList();
        return sorted.SequenceEqual(sortedTwice);
    }
}
```

No hand-written test cases. Conjecture generates random lists, runs the property 100 times, and if it fails, shrinks the input to the minimal failing case.

## Features

- **Automatic test generation** — generates random inputs from type-aware strategies
- **Intelligent shrinking** — finds the smallest failing input via byte-stream minimization
- **LINQ composition** — build complex strategies with `Select`, `Where`, `SelectMany`
- **All major frameworks** — xUnit v2, xUnit v3, NUnit, MSTest, Microsoft Testing Platform, F# Expecto
- **Source generators** — derive strategies for your types with `[Arbitrary]`
- **Roslyn analyzers** — catch common mistakes at compile time
- **Stateful testing** — model systems as state machines and explore command sequences
- **Targeted testing** — steer generation toward extremes with `Target.Maximize` / `Target.Minimize`
- **Recursive strategies** — generate bounded-depth trees and self-referential types
- **Example database** — persist failing inputs for automatic regression prevention
- **Reproduction export** — write a runnable `.cs` repro of any failing input via `ExportReproductionOnFailure`
- **Structured logging** — structured events for generation, shrinking, and targeting phases

## Install

```bash
# Pick the adapter for your test framework:
dotnet add package Conjecture.Xunit      # xUnit v2
dotnet add package Conjecture.Xunit.V3   # xUnit v3
dotnet add package Conjecture.NUnit      # NUnit
dotnet add package Conjecture.MSTest     # MSTest
```

## Get Started

- [Install](articles/how-to/install.md) — package names and requirements
- [Quick Start](articles/quick-start.md) — write your first property test in 5 minutes
- [Tutorials](articles/tutorials/01-your-first-property-test.md) — learn property-based testing step by step
- <xref:Conjecture.Core?text=API+Reference> — auto-generated from source
- [Porting Guide](articles/porting-guide.md) — coming from Python Hypothesis?
