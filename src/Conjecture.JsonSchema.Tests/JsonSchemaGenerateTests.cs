// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Conjecture.Core;

namespace Conjecture.JsonSchema.Tests;

public sealed class JsonSchemaGenerateTests
{
    [Fact]
    public void FromJsonSchema_IntegerSchemaText_ReturnsNonNullStrategy()
    {
        Strategy<JsonElement> strategy = Strategy.FromJsonSchema("""{"type": "integer"}""");
        Assert.NotNull(strategy);
    }

    [Fact]
    public void FromJsonSchema_IntegerSchemaText_GeneratesNumberValueKind()
    {
        Strategy<JsonElement> strategy = Strategy.FromJsonSchema("""{"type": "integer"}""");
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 20, 42UL);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Number, element.ValueKind);
        }
    }

    [Fact]
    public void FromJsonSchema_JsonElementOverload_GeneratesNumberValueKind()
    {
        using JsonDocument doc = JsonDocument.Parse("""{"type": "integer"}""");
        Strategy<JsonElement> strategy = Strategy.FromJsonSchema(doc.RootElement);
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 20, 42UL);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Number, element.ValueKind);
        }
    }

    [Fact]
    public void FromJsonSchema_FileInfoOverload_GeneratesNumberValueKind()
    {
        string tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, """{"type": "integer"}""");
            FileInfo schemaFile = new(tempPath);
            Strategy<JsonElement> strategy = Strategy.FromJsonSchema(schemaFile);
            IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 20, 42UL);
            foreach (JsonElement element in samples)
            {
                Assert.Equal(JsonValueKind.Number, element.ValueKind);
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void FromJsonSchema_InvalidJsonText_ThrowsJsonExceptionAtConstruction()
    {
        Assert.ThrowsAny<JsonException>(() => Strategy.FromJsonSchema("not valid json"));
    }
}