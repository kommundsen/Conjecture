# Contributing

Conjecture.NET is a port of Python's [Hypothesis](https://github.com/HypothesisWorks/hypothesis). Contributions are welcome.

## Development Setup

```bash
git clone https://github.com/<owner>/Conjecture.git
cd Conjecture
dotnet build src/
dotnet test src/
```

Requires **.NET 10 SDK**.

## Project Structure

```
src/
├── Conjecture.Core           # Core engine, strategies, settings
├── Conjecture.Xunit          # xUnit v2 adapter
├── Conjecture.Xunit.V3       # xUnit v3 adapter
├── Conjecture.NUnit          # NUnit adapter
├── Conjecture.MSTest          # MSTest adapter
├── Conjecture.Generators      # Source generator ([Arbitrary])
├── Conjecture.Analyzers       # Roslyn analyzer (CON100-105)
├── Conjecture.Tests           # Core unit tests
├── Conjecture.Xunit.Tests     # xUnit v2 adapter tests
├── Conjecture.Xunit.V3.Tests  # xUnit v3 adapter tests
├── Conjecture.NUnit.Tests     # NUnit adapter tests
├── Conjecture.MSTest.Tests    # MSTest adapter tests
├── Conjecture.Generators.Tests
├── Conjecture.Analyzers.Tests
├── Conjecture.SelfTests       # Dogfooding (tests Conjecture with Conjecture)
└── Conjecture.Benchmarks      # BenchmarkDotNet performance tests
```

## Architecture

- **Core engine** (`Conjecture.Core`): byte-stream-backed test case generation and shrinking. Strategies generate values by consuming bytes from `ConjectureData`. Shrinking minimizes the byte buffer.
- **Framework adapters**: thin layers that integrate `[Property]` with each test framework's discovery and execution pipeline. They share `SharedParameterStrategyResolver` for parameter resolution.
- **Source generator**: incremental Roslyn generator that creates `IStrategyProvider<T>` implementations from `[Arbitrary]`-annotated types.
- **Analyzers**: Roslyn analyzers that catch common mistakes at compile time.

## Code Conventions

- Follow `.editorconfig` rules (enforced at build via `EnforceCodeStyleInBuild`)
- XML doc comments (`///`) required on all public and protected members
- Warnings are errors (`TreatWarningsAsErrors = true`)
- Never use `.GetAwaiter().GetResult()` or `.Result` — make callers `async` instead
- File-scoped namespaces
- `sealed` on non-inheritable classes

## Running Tests

```bash
# All tests
dotnet test src/

# Specific test
dotnet test src/ --filter "FullyQualifiedName~SomeTestName"

# Benchmarks
dotnet run --project src/Conjecture.Benchmarks -c Release
```

## Architecture Decision Records

Design decisions are documented in [`docs/decisions/`](https://github.com/<owner>/Conjecture/tree/main/docs/decisions). Read these before proposing changes to core architecture. If your change involves a significant design choice, add an ADR.

## PR Checklist

- [ ] Tests pass: `dotnet test src/`
- [ ] Build clean (no warnings): `dotnet build src/`
- [ ] XML docs on new public/protected members
- [ ] ADR added if the change involves an architectural decision

## License

Source code is licensed under [MPL-2.0](https://mozilla.org/MPL/2.0/). NuGet packages are distributed under [MIT](https://opensource.org/licenses/MIT). See ADR-0031 in `docs/decisions/` for rationale.
