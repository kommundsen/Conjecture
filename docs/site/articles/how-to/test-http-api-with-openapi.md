# How to property-test an HTTP API endpoint using OpenAPI

Load an OpenAPI document and generate valid request bodies to property-test endpoint contracts.

## Prerequisites

Install `Conjecture.OpenApi`:

```bash
dotnet add package Conjecture.OpenApi
```

You need a Swagger/OpenAPI spec file (`.json` or `.yaml`) or a URL pointing to one.

## Steps

### 1. Load the OpenAPI document

```csharp
using Conjecture.OpenApi;

OpenApiDocument doc = await Strategy.FromOpenApi("swagger.json");
```

`FromOpenApi` accepts a file path (`string`), a `FileInfo`, or a `Uri` (for remote specs).

### 2. Get a strategy for a request body

```csharp
Strategy<JsonElement> requestStrategy = doc.RequestBody("POST", "/api/orders");
```

The method and path must match exactly as they appear in the spec.

### 3. Write the property test

# [xUnit v2](#tab/xunit-v2)

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Conjecture.Core;
using Conjecture.OpenApi;
using Conjecture.Xunit;
using Xunit;

public class OrderApiTests(HttpClient client) : IClassFixture<ApiFactory>
{
    private static OpenApiDocument? _doc;

    private static async Task<OpenApiDocument> GetDoc() =>
        _doc ??= await Strategy.FromOpenApi("swagger.json");

    [Property]
    public async Task CreateOrder_ValidRequest_Returns2xx(JsonElement body)
    {
        OpenApiDocument doc = await GetDoc();
        // Declare the strategy via [From] or use DataGen directly
        StringContent content = new(body.GetRawText(), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync("/api/orders", content);
        Assert.InRange((int)response.StatusCode, 200, 299);
    }
}
```

# [xUnit v3](#tab/xunit-v3)

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Conjecture.Core;
using Conjecture.OpenApi;
using Conjecture.Xunit.V3;
using Xunit;

public class OrderApiTests(HttpClient client) : IClassFixture<ApiFactory>
{
    [Property]
    public async Task CreateOrder_ValidRequest_Returns2xx()
    {
        OpenApiDocument doc = await Strategy.FromOpenApi("swagger.json");
        Strategy<JsonElement> strategy = doc.RequestBody("POST", "/api/orders");
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 20);
        foreach (JsonElement body in samples)
        {
            StringContent content = new(body.GetRawText(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync("/api/orders", content);
            Assert.InRange((int)response.StatusCode, 200, 299);
        }
    }
}
```

# [NUnit](#tab/nunit)

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Conjecture.Core;
using Conjecture.OpenApi;
using Conjecture.NUnit;
using NUnit.Framework;

[TestFixture]
public class OrderApiTests
{
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp() => _client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

    [Property]
    public async Task CreateOrder_ValidRequest_Returns2xx()
    {
        OpenApiDocument doc = await Strategy.FromOpenApi("swagger.json");
        Strategy<JsonElement> strategy = doc.RequestBody("POST", "/api/orders");
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 20);
        foreach (JsonElement body in samples)
        {
            StringContent content = new(body.GetRawText(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _client.PostAsync("/api/orders", content);
            Assert.That((int)response.StatusCode, Is.InRange(200, 299));
        }
    }
}
```

# [MSTest](#tab/mstest)

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Conjecture.Core;
using Conjecture.OpenApi;
using Conjecture.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class OrderApiTests
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri("http://localhost:5000") };

    [Property]
    public async Task CreateOrder_ValidRequest_Returns2xx()
    {
        OpenApiDocument doc = await Strategy.FromOpenApi("swagger.json");
        Strategy<JsonElement> strategy = doc.RequestBody("POST", "/api/orders");
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 20);
        foreach (JsonElement body in samples)
        {
            StringContent content = new(body.GetRawText(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _client.PostAsync("/api/orders", content);
            Assert.IsTrue((int)response.StatusCode is >= 200 and <= 299);
        }
    }
}
```

***

## Testing response shapes

Use `ResponseBody` to verify that the API returns data matching its own declared schema:

```csharp
Strategy<JsonElement> expectedShape = doc.ResponseBody("GET", "/api/orders/{id}", statusCode: 200);

// Fetch a real response and deserialise
JsonElement actual = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

// Assert the actual response has the expected fields
Assert.True(actual.TryGetProperty("orderId", out _));
```

## Testing path and query parameters

```csharp
Strategy<JsonElement> idStrategy = doc.PathParameter("GET", "/api/orders/{id}", "id");
```

> [!TIP]
> Cache the `OpenApiDocument` instance across tests — loading and parsing the spec on every test run is slow. Store it as a static field or use a test fixture.

> [!NOTE]
> Generated values conform to the JSON Schema constraints declared in the spec (`minLength`, `minimum`, `enum`, `$ref`, etc.). See [Schema strategies reference](../reference/schema-strategies.md) for the full constraint mapping.
