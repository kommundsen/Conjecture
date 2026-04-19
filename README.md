# Conjecture.NET

[![CI](https://github.com/kommundsen/Conjecture/actions/workflows/ci.yml/badge.svg)](https://github.com/kommundsen/Conjecture/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-ommundsen.dev-blue)](https://ommundsen.dev/Conjecture/)

[![NuGet: Conjecture.Core](https://img.shields.io/nuget/v/Conjecture.Core?label=Conjecture.Core)](https://www.nuget.org/packages/Conjecture.Core)
[![NuGet: Conjecture.Xunit](https://img.shields.io/nuget/v/Conjecture.Xunit?label=Conjecture.Xunit)](https://www.nuget.org/packages/Conjecture.Xunit)
[![NuGet: Conjecture.Xunit.V3](https://img.shields.io/nuget/v/Conjecture.Xunit.V3?label=Conjecture.Xunit.V3)](https://www.nuget.org/packages/Conjecture.Xunit.V3)
[![NuGet: Conjecture.NUnit](https://img.shields.io/nuget/v/Conjecture.NUnit?label=Conjecture.NUnit)](https://www.nuget.org/packages/Conjecture.NUnit)
[![NuGet: Conjecture.MSTest](https://img.shields.io/nuget/v/Conjecture.MSTest?label=Conjecture.MSTest)](https://www.nuget.org/packages/Conjecture.MSTest)
[![NuGet: Conjecture.TestingPlatform](https://img.shields.io/nuget/v/Conjecture.TestingPlatform?label=Conjecture.TestingPlatform)](https://www.nuget.org/packages/Conjecture.TestingPlatform)

**Property-based testing for .NET** — a ground-up port of Python's [Hypothesis](https://github.com/HypothesisWorks/hypothesis).

## What is it?

Write tests that describe *what* your code should do, and let Conjecture generate hundreds of random inputs to find the edge cases you'd never think of. When it finds a failure, it automatically **shrinks** the input to the smallest possible counterexample — no hand-written cases required.

## Install

Pick the adapter for your test framework:

```bash
dotnet add package Conjecture.Xunit      # xUnit v2
dotnet add package Conjecture.Xunit.V3   # xUnit v3
dotnet add package Conjecture.NUnit      # NUnit
dotnet add package Conjecture.MSTest     # MSTest
```

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

Run with `dotnet test`. Conjecture generates random lists, runs the property 100 times, and if it fails, shrinks the input to the minimal failing case.

## Features

- **Automatic test generation** — generates random inputs from type-aware strategies
- **Intelligent shrinking** — finds the smallest failing input via byte-stream minimization
- **LINQ composition** — build complex strategies with `Select`, `Where`, `SelectMany`
- **All major frameworks** — xUnit v2, xUnit v3, NUnit, MSTest
- **Source generators** — derive strategies for your types with `[Arbitrary]`
- **Roslyn analyzers** — catch common mistakes at compile time
- **Stateful testing** — model systems as state machines and explore command sequences
- **Targeted testing** — steer generation toward extremes with `Target.Maximize` / `Target.Minimize`
- **Recursive strategies** — generate bounded-depth trees and self-referential types
- **Example database** — persist failing inputs for automatic regression prevention
- **Structured logging** — structured events for generation, shrinking, and targeting phases

## Documentation

Full documentation is at **[ommundsen.dev/Conjecture](https://ommundsen.dev/Conjecture/)**:

- [Quick Start](https://ommundsen.dev/Conjecture/articles/quick-start.html) — write your first property test in 5 minutes
- [Tutorials](https://ommundsen.dev/Conjecture/articles/tutorials/01-your-first-property-test.html) — learn property-based testing step by step
- [API Reference](https://ommundsen.dev/Conjecture/api/) — auto-generated from source
- [Porting Guide](https://ommundsen.dev/Conjecture/articles/porting-guide.html) — coming from Python Hypothesis?
- [Changelog](CHANGELOG.md)

## Credit

This project is an attempt at a .NET port of [Hypothesis] for Python. The concept of this project builds on and would not be possible without the work of [David R. MacIver](https://www.drmaciver.com/) and [Zac Hatfield-Dodds](https://zhd.dev/), as well as the many other [authors](https://github.com/HypothesisWorks/hypothesis/blob/master/AUTHORS.rst) of the [Hypothesis] project.

## License

Source code: [MPL-2.0](LICENSE.txt) | NuGet packages: [MIT](LICENSE-MIT.txt)

[Hypothesis]: https://github.com/HypothesisWorks/hypothesis
