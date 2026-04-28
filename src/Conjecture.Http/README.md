# Conjecture.Http

HTTP interaction primitives for [Conjecture](https://github.com/kommundsen/Conjecture). Defines `HttpInteraction` (a serializable description of an HTTP call) and `IHttpTarget` (resolves an `HttpClient` per named resource) so you can write transport-agnostic property tests over HTTP services.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Http
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Http;
using Conjecture.Xunit;

public class ApiTests
{
    [Property]
    public async Task NeverReturns5xx(HttpInteraction request)
    {
        Strategy<HttpInteraction> strategy = Generate.Http("api")
            .Get("/items/{id}")
            .Build();

        await strategy.Sample().Response(target).AssertNot5xx();
    }
}
```

`HostHttpTarget` wraps an `IHost` (e.g. a `WebApplicationFactory`) so requests dispatch in-process. Compose `HttpInteraction` strategies with [`Conjecture.AspNetCore`](https://www.nuget.org/packages/Conjecture.AspNetCore) to derive valid and malformed requests directly from minimal-API and MVC route metadata.

## Types

| Type | Role |
|---|---|
| `HttpInteraction` | Readonly record: method, path, headers, body, named resource. |
| `IHttpTarget` | Resolves an `HttpClient` for a named resource. |
| `HostHttpTarget` | `IHttpTarget` over an `IHost` / `WebApplicationFactory`. |
| `HttpStrategyBuilder` | Fluent builder for `HttpInteraction` strategies — `Generate.Http(resource).Get/Post/Put/Delete/Patch(...).Build()`. |
| `HttpInvariantExtensions` | `AssertNot5xx`, `Assert4xx`, `AssertProblemDetailsShape`, `Response(target)`. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
