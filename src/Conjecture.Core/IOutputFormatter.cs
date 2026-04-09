// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Serializes a sequence of generated values to a <see cref="Stream"/>.</summary>
public interface IOutputFormatter
{
    /// <summary>Short identifier for this format (e.g. <c>"json"</c>, <c>"jsonl"</c>).</summary>
    string Name { get; }

    /// <summary>Writes all <paramref name="data"/> to <paramref name="output"/> in this formatter's encoding.</summary>
    Task WriteAsync<T>(IEnumerable<T> data, Stream output, CancellationToken ct = default);
}