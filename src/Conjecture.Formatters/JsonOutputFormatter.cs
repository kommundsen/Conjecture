// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Core;

namespace Conjecture.Formatters;

/// <summary>Writes a sequence of values to a stream as a JSON array.</summary>
public sealed class JsonOutputFormatter : IOutputFormatter
{
    /// <inheritdoc/>
    public string Name => "json";

    /// <inheritdoc/>
    public async Task WriteAsync<T>(IEnumerable<T> data, Stream output, CancellationToken ct = default)
    {
        await JsonSerializer.SerializeAsync(output, data, JsonFormatterOptions.Default, ct);
    }
}