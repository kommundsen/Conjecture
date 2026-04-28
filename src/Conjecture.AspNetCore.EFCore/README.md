# Conjecture.AspNetCore.EFCore

Satellite package that wires together [`Conjecture.AspNetCore`](https://www.nuget.org/packages/Conjecture.AspNetCore) and [`Conjecture.EFCore`](https://www.nuget.org/packages/Conjecture.EFCore) for property-based integration testing of ASP.NET Core applications backed by Entity Framework Core.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.AspNetCore.EFCore
```

## Usage

`AspNetCoreDbTarget<TContext>` resolves an EF Core context from a `WebApplicationFactory<TEntryPoint>`'s service provider and dispatches `DbInteraction` blocks against it. `AspNetCoreEFCoreInvariants` then enforces the cross-cutting invariants:

- **No partial writes on error** — failed requests must roll back any rows they staged.
- **Cascade correctness** — DELETE responses align with EF Core's modelled cascade behaviour.
- **Idempotency** — endpoints opted in via `MarkIdempotent(...)` produce the same DB state on a retry.

```csharp
using Conjecture.AspNetCore.EFCore;
using Conjecture.EFCore;
using Conjecture.Http;
using Microsoft.AspNetCore.Mvc.Testing;

WebApplicationFactory<Program> factory = new();
HttpClient client = factory.CreateClient();
HostHttpTarget http = new(factory.Services.GetRequiredService<IHost>(), client);
await using AspNetCoreDbTarget<MyDbContext> db = new(factory.Services.GetRequiredService<IHost>(), "mydb");

AspNetCoreEFCoreInvariants invariants = new(http, db);
await invariants.AssertNoPartialWritesOnErrorAsync(
    (c, ct) => c.PostAsJsonAsync("/orders", new { Quantity = -1 }, ct));
```

## Design

See [ADR 0066](https://github.com/kommundsen/Conjecture/blob/main/docs/decisions/0066-conjecture-aspnetcore-efcore-package-design.md) for design rationale.

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
