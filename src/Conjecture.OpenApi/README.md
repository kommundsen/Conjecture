# Conjecture.OpenApi

OpenAPI-driven strategy generation for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Loads an OpenAPI 3 document (file, URI, or `FileInfo`) and produces `JsonElement` strategies for path parameters, query parameters, request bodies, and response bodies, so you can fuzz any HTTP endpoint that has a published spec.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.OpenApi
```

## Usage

```csharp
using System.Text.Json;
using Conjecture.Core;
using Conjecture.OpenApi;

OpenApiDocument doc = await Strategy.FromOpenApi("openapi.json");

Strategy<JsonElement> idParam = doc.PathParameter("get", "/orders/{id}", "id");
Strategy<JsonElement> orderBody = doc.RequestBody("post", "/orders");
Strategy<JsonElement> okResponse = doc.ResponseBody("get", "/orders/{id}", statusCode: 200);

JsonElement sampleBody = orderBody.Sample();
```

Pair with [`Conjecture.AspNetCore`](https://www.nuget.org/packages/Conjecture.AspNetCore) — `AspNetCoreRequestBuilder.FromOpenApi(doc)` substitutes the route-discovery step with the OpenAPI document, useful when the contract is canonical and the routing isn't.

## Types

| Type | Role |
|---|---|
| `OpenApiDocument` | Loaded spec; produces strategies for parameters and bodies. |
| `OpenApiDocument.PathParameter(method, path, name)` | Strategy for a path placeholder. |
| `OpenApiDocument.QueryParameter(method, path, name)` | Strategy for a query parameter. |
| `OpenApiDocument.RequestBody(method, path)` | Strategy for the request body. |
| `OpenApiDocument.ResponseBody(method, path, statusCode)` | Strategy for the response body of a status code. |
| `Strategy.FromOpenApi(string \| FileInfo \| Uri)` | Loads the spec and returns an `OpenApiDocument`. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)