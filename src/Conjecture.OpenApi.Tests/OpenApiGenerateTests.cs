// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Conjecture.Core;

namespace Conjecture.OpenApi.Tests;

public sealed class OpenApiGenerateTests
{
    // Minimal Petstore-style OpenAPI 3.0 document used across all tests.
    private const string PetstoreJson = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Petstore", "version": "1.0" },
          "paths": {
            "/pets": {
              "get": {
                "parameters": [
                  {
                    "name": "limit",
                    "in": "query",
                    "schema": { "type": "integer", "minimum": 1 }
                  }
                ],
                "responses": {
                  "200": {
                    "description": "A list of pets",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "array",
                          "items": {
                            "type": "object",
                            "required": ["id", "name"],
                            "properties": {
                              "id":  { "type": "integer" },
                              "name": { "type": "string" },
                              "tag":  { "type": "string" }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              },
              "post": {
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "required": ["name"],
                        "properties": {
                          "name": { "type": "string" },
                          "tag":  { "type": "string" }
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
                        "schema": {
                          "type": "object",
                          "required": ["id", "name"],
                          "properties": {
                            "id":   { "type": "integer" },
                            "name": { "type": "string" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            },
            "/pets/{petId}": {
              "get": {
                "parameters": [
                  {
                    "name": "petId",
                    "in": "path",
                    "required": true,
                    "schema": { "type": "integer", "minimum": 1 }
                  }
                ],
                "responses": {
                  "200": {
                    "description": "A single pet",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "required": ["id", "name"],
                          "properties": {
                            "id":   { "type": "integer" },
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

    private static string WriteTempFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"petstore_{System.Guid.NewGuid():N}.json");
        File.WriteAllText(path, PetstoreJson);
        return path;
    }

    [Fact]
    public async Task FromOpenApi_ValidFilePath_ReturnsNonNullDocument()
    {
        string path = WriteTempFile();
        try
        {
            OpenApiDocument doc = await Generate.FromOpenApi(path);
            Assert.NotNull(doc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FromOpenApi_ValidFileInfo_ReturnsNonNullDocument()
    {
        string path = WriteTempFile();
        try
        {
            FileInfo file = new(path);
            OpenApiDocument doc = await Generate.FromOpenApi(file);
            Assert.NotNull(doc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FromOpenApi_ValidFileUri_ReturnsNonNullDocument()
    {
        string path = WriteTempFile();
        try
        {
            Uri uri = new(path);
            OpenApiDocument doc = await Generate.FromOpenApi(uri);
            Assert.NotNull(doc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RequestBody_PostPets_ReturnsStrategy()
    {
        string path = WriteTempFile();
        try
        {
            OpenApiDocument doc = await Generate.FromOpenApi(path);
            Strategy<JsonElement> strategy = doc.RequestBody("POST", "/pets");
            Assert.NotNull(strategy);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RequestBody_PostPets_GeneratesObjectsWithNameProperty()
    {
        string path = WriteTempFile();
        try
        {
            OpenApiDocument doc = await Generate.FromOpenApi(path);
            Strategy<JsonElement> strategy = doc.RequestBody("POST", "/pets");
            IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 20, 42UL);

            foreach (JsonElement element in samples)
            {
                Assert.Equal(JsonValueKind.Object, element.ValueKind);
                Assert.True(element.TryGetProperty("name", out _), "Expected 'name' property");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResponseBody_GetPets_200_GeneratesArrayElements()
    {
        string path = WriteTempFile();
        try
        {
            OpenApiDocument doc = await Generate.FromOpenApi(path);
            Strategy<JsonElement> strategy = doc.ResponseBody("GET", "/pets", 200);
            Assert.NotNull(strategy);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PathParameter_GetPetById_PetId_GeneratesPositiveIntegers()
    {
        string path = WriteTempFile();
        try
        {
            OpenApiDocument doc = await Generate.FromOpenApi(path);
            Strategy<JsonElement> strategy = doc.PathParameter("GET", "/pets/{petId}", "petId");
            IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 20, 42UL);

            foreach (JsonElement element in samples)
            {
                Assert.Equal(JsonValueKind.Number, element.ValueKind);
                Assert.True(element.GetInt64() >= 1, $"petId must be >= 1 but was {element.GetInt64()}");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task QueryParameter_GetPets_Limit_GeneratesPositiveIntegers()
    {
        string path = WriteTempFile();
        try
        {
            OpenApiDocument doc = await Generate.FromOpenApi(path);
            Strategy<JsonElement> strategy = doc.QueryParameter("GET", "/pets", "limit");
            IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 20, 42UL);

            foreach (JsonElement element in samples)
            {
                Assert.Equal(JsonValueKind.Number, element.ValueKind);
                Assert.True(element.GetInt64() >= 1, $"limit must be >= 1 but was {element.GetInt64()}");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FromOpenApi_NonExistentFilePath_ThrowsException()
    {
        await Assert.ThrowsAnyAsync<Exception>(
            static async () => await Generate.FromOpenApi("/nonexistent/path/petstore.json"));
    }
}