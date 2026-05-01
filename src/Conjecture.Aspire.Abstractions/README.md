# Conjecture.Aspire.Abstractions

Extension contracts for [Conjecture.Aspire](https://www.nuget.org/packages/Conjecture.Aspire) satellites. Reference this package when building a `Conjecture.Aspire.{Tech}` bridge (e.g. `Conjecture.Aspire.Messaging`). End-user test code should reference [`Conjecture.Aspire`](https://www.nuget.org/packages/Conjecture.Aspire) instead.

## Who is this for?

Authors wiring a new transport into an Aspire-based property test. The existing satellites — [`Conjecture.Aspire.Http`](https://www.nuget.org/packages/Conjecture.Aspire.Http) and [`Conjecture.Aspire.EFCore`](https://www.nuget.org/packages/Conjecture.Aspire.EFCore) — follow this pattern.

## Install

```
dotnet add package Conjecture.Aspire.Abstractions
```

## Types

| Type | Role |
|---|---|
| `IAspireAppFixture` | Manages the `DistributedApplication` lifecycle. Subclass and override `StartAsync`, `ResetAsync`, and optionally `WaitForHealthyAsync`. |
| `ISnapshotLabel` | Marker interface for interaction DTOs that carry a human-readable label for snapshot trace output. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
