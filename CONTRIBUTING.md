# Contributing to Conjecture.NET

Thank you for your interest in contributing!

## Prerequisites

- .NET 10 SDK
- An editor with EditorConfig support

## Build & Test

```bash
dotnet build src/
dotnet test src/
dotnet test src/ --filter "FullyQualifiedName~SomeTest"
```

## Development Workflow

This project uses TDD. Each feature follows **Red → Green → Refactor**:

1. Write a failing test that describes the desired behavior.
2. Write the minimal production code to make it pass.
3. Refactor without changing observable behavior, keeping tests green.

## Submitting Changes

1. Fork the repository and create a branch from `main`.
2. Suggested branch prefixes: `feat/`, `fix/`, `chore/`, `docs/`.
3. Ensure all tests pass (`dotnet test src/`).
4. Open a pull request against `main` with a clear description.

## License

By submitting a contribution you agree that your changes will be licensed under the [Mozilla Public License 2.0](LICENSE.txt) (source code) and [MIT](LICENSE-MIT.txt) (NuGet packages), consistent with the rest of the project.
