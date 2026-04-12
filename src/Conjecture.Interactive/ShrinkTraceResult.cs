// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Interactive;

/// <summary>The result of a shrink trace: the ordered steps and an HTML rendering.</summary>
public sealed class ShrinkTraceResult<T>(IReadOnlyList<ShrinkStep<T>> steps, string html)
{
    /// <summary>The shrink steps, from initial failing value through each accepted reduction.</summary>
    public IReadOnlyList<ShrinkStep<T>> Steps { get; } = steps;

    /// <summary>An HTML table rendering of the shrink trace.</summary>
    public string Html { get; } = html;
}
