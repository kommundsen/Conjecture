# Conjecture.AspNetCore

Property-based testing for ASP.NET Core minimal APIs and MVC controllers, built on [Conjecture](https://github.com/kommundsen/Conjecture). Walks `EndpointDataSource` and `IApiDescriptionGroupCollectionProvider` to discover routes, then synthesises typed valid and malformed `HttpInteraction` strategies dispatched in-process via `WebApplicationFactory`.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.AspNetCore
```

## Usage

```csharp
using Conjecture.AspNetCore;
using Conjecture.Core;
using Conjecture.Http;
using Conjecture.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;

public class ApiSafetyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ApiSafetyTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Property]
    public async Task NeverReturns5xx(HttpInteraction request)
    {
        HttpClient client = this.factory.CreateClient();
        Strategy<HttpInteraction> strategy = Strategy
            .AspNetCoreRequests(this.factory.Services.GetRequiredService<IHost>(), client)
            .Build();

        HostHttpTarget target = new(this.factory.Services.GetRequiredService<IHost>(), client);
        await request.Response(target).AssertNot5xx();
    }
}
```

`MalformedRequestsOnly()` and `ValidRequestsOnly()` switch the generated request distribution; `ExcludeEndpoints(...)` skips routes whose metadata you don't want covered (e.g. `/admin`).

## Design

See [ADR 0063](https://github.com/kommundsen/Conjecture/blob/main/docs/decisions/0063-conjecture-aspnetcore-package-design.md) for the metadata-driven request synthesis design.

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
