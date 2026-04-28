![Conjecture Banner](assets/conjecture-banner.png)

[![CI](https://github.com/kommundsen/Conjecture/actions/workflows/ci.yml/badge.svg)](https://github.com/kommundsen/Conjecture/actions/workflows/ci.yml)
[![Docs](https://github.com/kommundsen/Conjecture/actions/workflows/docs.yml/badge.svg)](https://github.com/kommundsen/Conjecture/actions/workflows/docs.yml)
[![Release](https://github.com/kommundsen/Conjecture/actions/workflows/release.yml/badge.svg)](https://github.com/kommundsen/Conjecture/actions/workflows/release.yml)
[![Docs](https://img.shields.io/badge/docs-ommundsen.dev-blue)](https://ommundsen.dev/Conjecture/)

[![Conjecture.Core](https://img.shields.io/nuget/v/Conjecture.Core?label=Conjecture.Core)](https://www.nuget.org/packages/Conjecture.Core)
[![Conjecture.Xunit](https://img.shields.io/nuget/v/Conjecture.Xunit?label=Xunit)](https://www.nuget.org/packages/Conjecture.Xunit)
[![Conjecture.Xunit.V3](https://img.shields.io/nuget/v/Conjecture.Xunit.V3?label=Xunit.V3)](https://www.nuget.org/packages/Conjecture.Xunit.V3)
[![Conjecture.NUnit](https://img.shields.io/nuget/v/Conjecture.NUnit?label=NUnit)](https://www.nuget.org/packages/Conjecture.NUnit)
[![Conjecture.MSTest](https://img.shields.io/nuget/v/Conjecture.MSTest?label=MSTest)](https://www.nuget.org/packages/Conjecture.MSTest)
[![Conjecture.TestingPlatform](https://img.shields.io/nuget/v/Conjecture.TestingPlatform?label=TestingPlatform)](https://www.nuget.org/packages/Conjecture.TestingPlatform)

## What is it?

[Hypothesis](https://github.com/HypothesisWorks/hypothesis)'s engine, reimagined for idiomatic C#. Write tests that describe *what* your code should do, and let Conjecture generate hundreds of random inputs to find the edge cases you'd never think of. When it finds a failure, it automatically **shrinks** the input to the smallest possible counterexample — no hand-written cases required.

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
        List<int> sorted = items.OrderBy(x => x).ToList();
        List<int> sortedTwice = sorted.OrderBy(x => x).ToList();
        return sorted.SequenceEqual(sortedTwice);
    }
}
```

Run with `dotnet test`. Conjecture generates random lists, runs the property 100 times, and if it fails, shrinks the input to the minimal failing case.

## Features

- **Automatic test generation** — generates random inputs from type-aware strategies
- **Intelligent shrinking** — finds the smallest failing input via byte-stream minimization
- **LINQ composition** — build complex strategies with `Select`, `Where`, `SelectMany`
- **All major test frameworks** — xUnit v2, xUnit v3, NUnit, MSTest, Microsoft Testing Platform
- **F# support** — idiomatic `Gen<'a>` wrapper plus an Expecto integration
- **Source generators** — derive strategies for your types with `[Arbitrary]`
- **Roslyn analyzers** — catch common mistakes at compile time
- **Stateful testing** — model systems as state machines and explore command sequences
- **Targeted testing** — steer generation toward extremes with `Target.Maximize` / `Target.Minimize`
- **Recursive strategies** — generate bounded-depth trees and self-referential types
- **Example database** — persist failing inputs for automatic regression prevention
- **Structured logging** — structured events for generation, shrinking, and targeting phases
- **HTTP / gRPC / messaging / EF Core / ASP.NET Core / Aspire integrations** — model real distributed systems as `IInteractionTarget`s and dispatch composed interactions through one state machine
- **MCP server + dotnet tool** — generate strategies and standalone test data from the CLI or AI assistants

## Packages

| Package | Purpose |
|---|---|
| **Test adapters** | |
| [`Conjecture.Core`](https://www.nuget.org/packages/Conjecture.Core) | Core engine, analyzers, source generator. Required by every adapter. |
| [`Conjecture.Xunit`](https://www.nuget.org/packages/Conjecture.Xunit) | xUnit v2 adapter. |
| [`Conjecture.Xunit.V3`](https://www.nuget.org/packages/Conjecture.Xunit.V3) | xUnit v3 adapter. |
| [`Conjecture.NUnit`](https://www.nuget.org/packages/Conjecture.NUnit) | NUnit adapter. |
| [`Conjecture.MSTest`](https://www.nuget.org/packages/Conjecture.MSTest) | MSTest adapter. |
| [`Conjecture.TestingPlatform`](https://www.nuget.org/packages/Conjecture.TestingPlatform) | Microsoft Testing Platform adapter. |
| **Domain strategies** | |
| [`Conjecture.Time`](https://www.nuget.org/packages/Conjecture.Time) | Boundary-aware `DateTimeOffset`, DST/leap edges, `FakeTimeProvider`. |
| [`Conjecture.Money`](https://www.nuget.org/packages/Conjecture.Money) | ISO 4217 currency codes, scaled decimal amounts, rounding modes. |
| [`Conjecture.Regex`](https://www.nuget.org/packages/Conjecture.Regex) | Regex-driven strings, common formats (URL/UUID/email/IP), ReDoS hunter. |
| [`Conjecture.JsonSchema`](https://www.nuget.org/packages/Conjecture.JsonSchema) | Strategies that conform to a JSON Schema. |
| [`Conjecture.Protobuf`](https://www.nuget.org/packages/Conjecture.Protobuf) | Strategies shaped by a Protobuf `MessageDescriptor`. |
| **Output & visualization** | |
| [`Conjecture.Formatters`](https://www.nuget.org/packages/Conjecture.Formatters) | JSON / NDJSON formatters for standalone data generation. |
| [`Conjecture.Interactive`](https://www.nuget.org/packages/Conjecture.Interactive) | Strategy preview, histograms, shrink traces (plain text). |
| [`Conjecture.LinqPad`](https://www.nuget.org/packages/Conjecture.LinqPad) | LINQPad rich HTML formatters and shrink-trace visualizers. |
| **Interactions** | |
| [`Conjecture.Interactions`](https://www.nuget.org/packages/Conjecture.Interactions) | Transport-agnostic abstractions: `IInteraction`, `InteractionStateMachine<TState>`. |
| [`Conjecture.Http`](https://www.nuget.org/packages/Conjecture.Http) | `HttpInteraction` and `IHttpTarget`. |
| [`Conjecture.Grpc`](https://www.nuget.org/packages/Conjecture.Grpc) | `GrpcInteraction` and `IGrpcTarget` covering all four RPC modes. |
| [`Conjecture.Messaging`](https://www.nuget.org/packages/Conjecture.Messaging) | Transport-agnostic message bus interactions + in-memory bus. |
| [`Conjecture.Messaging.AzureServiceBus`](https://www.nuget.org/packages/Conjecture.Messaging.AzureServiceBus) | Azure Service Bus adapter. |
| [`Conjecture.Messaging.RabbitMq`](https://www.nuget.org/packages/Conjecture.Messaging.RabbitMq) | RabbitMQ adapter. |
| **Web & data** | |
| [`Conjecture.AspNetCore`](https://www.nuget.org/packages/Conjecture.AspNetCore) | Metadata-driven request synthesis for ASP.NET Core minimal APIs and MVC. |
| [`Conjecture.EFCore`](https://www.nuget.org/packages/Conjecture.EFCore) | Entity-graph strategies, roundtrip + migration invariants, `IDbTarget`. |
| [`Conjecture.AspNetCore.EFCore`](https://www.nuget.org/packages/Conjecture.AspNetCore.EFCore) | Bridge for ASP.NET Core + EF Core integration tests. |
| [`Conjecture.OpenApi`](https://www.nuget.org/packages/Conjecture.OpenApi) | Strategies derived from an OpenAPI 3 document. |
| **Distributed** | |
| [`Conjecture.Aspire`](https://www.nuget.org/packages/Conjecture.Aspire) | Stateful property tests against .NET Aspire `DistributedApplication`. |
| [`Conjecture.Aspire.EFCore`](https://www.nuget.org/packages/Conjecture.Aspire.EFCore) | Aspire + EF Core bridge with composite invariants. |
| **Tooling** | |
| [`Conjecture.Mcp`](https://www.nuget.org/packages/Conjecture.Mcp) | Model Context Protocol server exposing Conjecture to AI assistants. |
| `Conjecture.Tool` | `dotnet conjecture` CLI for standalone data generation (in repo). |
| **F#** | |
| [`Conjecture.FSharp`](https://www.nuget.org/packages/Conjecture.FSharp) | Idiomatic F# `Gen<'a>` wrappers and a `PropertyRunner`. |
| [`Conjecture.FSharp.Expecto`](https://www.nuget.org/packages/Conjecture.FSharp.Expecto) | Expecto integration for `Conjecture.FSharp`. |

## Documentation

Full documentation is at **[ommundsen.dev/Conjecture](https://ommundsen.dev/Conjecture/)**:

- [Quick Start](https://ommundsen.dev/Conjecture/articles/quick-start.html) — write your first property test in 5 minutes
- [Tutorials](https://ommundsen.dev/Conjecture/articles/tutorials/01-your-first-property-test.html) — learn property-based testing step by step
- [API Reference](https://ommundsen.dev/Conjecture/api/) — auto-generated from source
- [Porting Guide](https://ommundsen.dev/Conjecture/articles/porting-guide.html) — coming from Python Hypothesis?
- [Changelog](CHANGELOG.md)

## Credit

Conjecture is a .NET implementation of [Hypothesis]'s property-based testing engine. The shrinking algorithm, choice-sequence IR, targeting, and example database all derive from the work of [David R. MacIver](https://www.drmaciver.com/), [Zac Hatfield-Dodds](https://zhd.dev/), and the broader Hypothesis [contributors](https://github.com/HypothesisWorks/hypothesis/blob/master/AUTHORS.rst). The C# API surface, source generators, and analyzer integrations are original to this project.

## License

Source code: [MPL-2.0](LICENSE.txt) | NuGet packages: [MIT](LICENSE-MIT.txt)

[Hypothesis]: https://github.com/HypothesisWorks/hypothesis
