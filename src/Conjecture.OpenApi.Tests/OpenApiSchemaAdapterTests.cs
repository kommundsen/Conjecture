// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text.Json;

using Conjecture.Core;
using Conjecture.OpenApi;

using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace Conjecture.OpenApi.Tests;

public sealed class OpenApiSchemaAdapterTests
{
    private const string SimpleOpenApiJson = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Test", "version": "1.0" },
          "paths": {
            "/items": {
              "post": {
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "required": ["name", "count"],
                        "properties": {
                          "name": { "type": "string" },
                          "count": { "type": "integer", "minimum": 1 }
                        }
                      }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "Created",
                    "content": {
                      "application/json": {
                        "schema": { "type": "string" }
                      }
                    }
                  }
                }
              },
              "get": {
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "array",
                          "items": { "type": "string" }
                        }
                      }
                    }
                  }
                }
              }
            },
            "/items/{id}": {
              "get": {
                "parameters": [
                  {
                    "name": "id",
                    "in": "path",
                    "required": true,
                    "schema": { "type": "integer", "minimum": 1 }
                  },
                  {
                    "name": "expand",
                    "in": "query",
                    "schema": { "type": "boolean" }
                  }
                ],
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "id": { "type": "integer" },
                            "name": { "type": "string" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private const string OpenApiWithRefJson = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Test", "version": "1.0" },
          "components": {
            "schemas": {
              "Item": {
                "type": "object",
                "required": ["id"],
                "properties": {
                  "id": { "type": "integer" },
                  "label": { "type": "string" }
                }
              }
            }
          },
          "paths": {
            "/things": {
              "post": {
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": { "$ref": "#/components/schemas/Item" }
                    }
                  }
                },
                "responses": {
                  "200": { "description": "OK" }
                }
              }
            }
          }
        }
        """;

    private static OpenApiDocument ParseDocument(string json)
    {
        OpenApiStringReader reader = new();
        Microsoft.OpenApi.Models.OpenApiDocument raw = reader.Read(json, out _);
        return new OpenApiDocument(raw);
    }

    private static IReadOnlyList<T> Sample<T>(Strategy<T> strategy, int count = 20, ulong seed = 42UL)
    {
        return DataGen.Sample(strategy, count, seed);
    }

    [Fact]
    public void RequestBody_PostItems_GeneratesObjectsWithNameAndCount()
    {
        OpenApiDocument document = ParseDocument(SimpleOpenApiJson);
        OpenApiSchemaAdapter adapter = new(document);
        Strategy<JsonElement> strategy = adapter.RequestBody("POST", "/items");

        IReadOnlyList<JsonElement> samples = Sample(strategy);

        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
            Assert.True(element.TryGetProperty("name", out JsonElement name), "Expected 'name' property");
            Assert.Equal(JsonValueKind.String, name.ValueKind);
            Assert.True(element.TryGetProperty("count", out JsonElement count), "Expected 'count' property");
            Assert.Equal(JsonValueKind.Number, count.ValueKind);
            Assert.True(count.GetInt64() >= 1, $"count must be >= 1 but was {count.GetInt64()}");
        }
    }

    [Fact]
    public void ResponseBody_GetItemsById_200_GeneratesObjectsWithIdAndName()
    {
        OpenApiDocument document = ParseDocument(SimpleOpenApiJson);
        OpenApiSchemaAdapter adapter = new(document);
        Strategy<JsonElement> strategy = adapter.ResponseBody("GET", "/items/{id}", 200);

        IReadOnlyList<JsonElement> samples = Sample(strategy);

        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
        }
    }

    [Fact]
    public void PathParameter_GetItemsById_Id_GeneratesIntegersAtLeastOne()
    {
        OpenApiDocument document = ParseDocument(SimpleOpenApiJson);
        OpenApiSchemaAdapter adapter = new(document);
        Strategy<JsonElement> strategy = adapter.PathParameter("GET", "/items/{id}", "id");

        IReadOnlyList<JsonElement> samples = Sample(strategy);

        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Number, element.ValueKind);
            Assert.True(element.GetInt64() >= 1, $"id must be >= 1 but was {element.GetInt64()}");
        }
    }

    [Fact]
    public void QueryParameter_GetItemsById_Expand_GeneratesBooleans()
    {
        OpenApiDocument document = ParseDocument(SimpleOpenApiJson);
        OpenApiSchemaAdapter adapter = new(document);
        Strategy<JsonElement> strategy = adapter.QueryParameter("GET", "/items/{id}", "expand");

        IReadOnlyList<JsonElement> samples = Sample(strategy);

        foreach (JsonElement element in samples)
        {
            Assert.True(
                element.ValueKind is JsonValueKind.True or JsonValueKind.False,
                $"Expected boolean but got {element.ValueKind}");
        }
    }

    [Fact]
    public void RequestBody_UnknownPath_ThrowsKeyNotFoundException()
    {
        OpenApiDocument document = ParseDocument(SimpleOpenApiJson);
        OpenApiSchemaAdapter adapter = new(document);

        Assert.Throws<KeyNotFoundException>(() => adapter.RequestBody("POST", "/nonexistent"));
    }

    [Fact]
    public void RequestBody_UnknownMethod_ThrowsKeyNotFoundException()
    {
        OpenApiDocument document = ParseDocument(SimpleOpenApiJson);
        OpenApiSchemaAdapter adapter = new(document);

        Assert.Throws<KeyNotFoundException>(() => adapter.RequestBody("DELETE", "/items"));
    }

    [Fact]
    public void RequestBody_WithComponentRef_ResolvesAndGeneratesObjects()
    {
        OpenApiDocument document = ParseDocument(OpenApiWithRefJson);
        OpenApiSchemaAdapter adapter = new(document);
        Strategy<JsonElement> strategy = adapter.RequestBody("POST", "/things");

        IReadOnlyList<JsonElement> samples = Sample(strategy);

        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
            Assert.True(element.TryGetProperty("id", out JsonElement id), "Expected 'id' property from resolved $ref");
            Assert.Equal(JsonValueKind.Number, id.ValueKind);
        }
    }

    [Theory]
    [InlineData("POST", "/items", 5)]
    [InlineData("GET", "/items/{id}", 200)]
    public void ResponseBody_KnownOperations_ReturnsNonNullStrategy(string method, string path, int statusCode)
    {
        OpenApiDocument document = ParseDocument(SimpleOpenApiJson);
        OpenApiSchemaAdapter adapter = new(document);
        Strategy<JsonElement> strategy = adapter.ResponseBody(method, path, statusCode);

        Assert.NotNull(strategy);
    }
}
