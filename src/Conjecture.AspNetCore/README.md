# Conjecture.AspNetCore

Property-based testing for ASP.NET Core minimal APIs and MVC controllers, built on [Conjecture.NET](https://github.com/kommundsen/Conjecture).

Walks `EndpointDataSource` and `IApiDescriptionGroupCollectionProvider` to discover routes, synthesises typed valid and malformed `HttpInteraction` strategies, and dispatches them through `Conjecture.Http`'s `HostHttpTarget`.

```csharp
[Property]
public async Task NeverReturns5xx(HttpInteraction request)
{
    HttpResponseMessage resp = await request.Response(target);
    await Task.FromResult(resp).AssertNot5xx();
}
```

See [ADR 0063](../docs/decisions/0063-conjecture-aspnetcore-package-design.md) for design.
