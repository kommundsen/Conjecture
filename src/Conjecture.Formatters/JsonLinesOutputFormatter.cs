// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Core;

namespace Conjecture.Formatters;

/// <summary>Writes a sequence of values to a stream as newline-delimited JSON (NDJSON).</summary>
public sealed class JsonLinesOutputFormatter : IOutputFormatter
{
    /// <inheritdoc/>
    public string Name => "jsonl";

    /// <inheritdoc/>
    public async Task WriteAsync<T>(IEnumerable<T> data, Stream output, CancellationToken ct = default)
    {
        using StreamWriter writer = new(output, leaveOpen: true);
        foreach (T item in data)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(item, JsonFormatterOptions.Default).AsMemory(), ct);
        }

        await writer.FlushAsync(ct);
    }
}