# Conjecture.Http

HTTP interaction layer for [Conjecture.NET](https://github.com/kommundsen/Conjecture).

## Types

- `HttpInteraction` — readonly record struct describing an HTTP call addressed to a named resource.
- `IHttpTarget` — `IInteractionTarget` that resolves an `HttpClient` per resource and dispatches `HttpInteraction` as `HttpRequestMessage`, returning `HttpResponseMessage`.
