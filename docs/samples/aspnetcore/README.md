# Conjecture.AspNetCore samples

Two interactive samples demonstrating `Conjecture.AspNetCore` outside of a test framework:

- [`OrdersApiPropertyTests.dib`](OrdersApiPropertyTests.dib) — .NET Interactive polyglot notebook. Open in VS Code with the **Polyglot Notebooks** extension or in Jupyter via the .NET kernel. Runs the full property cell-by-cell with the kernel's cancellation token. Replace the inline `MapGet` calls with your real `WebApplicationFactory<TEntryPoint>` to drive your own API.
- [`OrdersApiPropertyTests.linq`](OrdersApiPropertyTests.linq) — LinqPad query. Press **F5** to run, **Cancel** to abort. `Util.KeepRunning()` keeps the script alive until the async runner completes; `QueryCancelToken` propagates the cancel button into Conjecture's deadline.

Both samples produce the same shape: build an in-process minimal-API host, extract `IHost` + `HttpClient`, build a `Strategy<HttpInteraction>` via `Strategy.AspNetCoreRequests`, then assert the never-5xx invariant via `Property.ForAll`. The fixture lifetime is the only thing that differs from a test-framework example.

## Related

- [Test ASP.NET Core endpoints — how-to](../../site/articles/how-to/test-aspnetcore-endpoints.md)
- [Wire AspNetCore into each test framework](../../site/articles/how-to/test-aspnetcore-framework-wiring.md)
- [`AspNetCoreRequestBuilder` reference](../../site/articles/reference/aspnetcore-request-builder.md)
- [ADR 0063 — Conjecture.AspNetCore package design](../../decisions/0063-conjecture-aspnetcore-package-design.md)
