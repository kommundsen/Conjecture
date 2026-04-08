// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text;
using System.Text.Json;
using Conjecture.Formatters;

namespace Conjecture.Formatters.Tests;

public class JsonLinesOutputFormatterTests
{
    [Fact]
    public void Name_IsJsonl()
    {
        var formatter = new JsonLinesOutputFormatter();

        Assert.Equal("jsonl", formatter.Name);
    }

    [Fact]
    public async Task WriteAsync_WritesOneLinePerRecord()
    {
        var formatter = new JsonLinesOutputFormatter();
        using var stream = new MemoryStream();

        await formatter.WriteAsync([1, 2, 3], stream);

        var lines = Encoding.UTF8.GetString(stream.ToArray()).Split('\n').Where(l => l.Length > 0).ToArray();
        Assert.Equal(3, lines.Length);
        foreach (string line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        }
    }

    [Fact]
    public async Task WriteAsync_EmptyInput_WritesNothing()
    {
        var formatter = new JsonLinesOutputFormatter();
        using var stream = new MemoryStream();

        await formatter.WriteAsync(Enumerable.Empty<int>(), stream);

        Assert.Equal(0, stream.Length);
    }
}
