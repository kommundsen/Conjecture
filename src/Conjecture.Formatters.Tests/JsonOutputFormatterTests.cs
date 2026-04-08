// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text;
using System.Text.Json;
using Conjecture.Formatters;

namespace Conjecture.Formatters.Tests;

public class JsonOutputFormatterTests
{
    [Fact]
    public void Name_IsJson()
    {
        var formatter = new JsonOutputFormatter();

        Assert.Equal("json", formatter.Name);
    }

    [Fact]
    public async Task WriteAsync_WritesValidJsonArray()
    {
        var formatter = new JsonOutputFormatter();
        using var stream = new MemoryStream();

        await formatter.WriteAsync([1, 2, 3], stream);

        stream.Position = 0;
        using var doc = await JsonDocument.ParseAsync(stream);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task WriteAsync_EmptyInput_WritesEmptyArray()
    {
        var formatter = new JsonOutputFormatter();
        using var stream = new MemoryStream();

        await formatter.WriteAsync(Enumerable.Empty<int>(), stream);

        Assert.Equal("[]", Encoding.UTF8.GetString(stream.ToArray()));
    }
}
