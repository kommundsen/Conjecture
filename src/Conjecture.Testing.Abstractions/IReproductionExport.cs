// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.ComponentModel;

namespace Conjecture.Abstractions.Testing;

/// <summary>
/// Optional interface for <c>[Property]</c> attributes that support exporting
/// reproduction files on test failure. Implement alongside <see cref="IPropertyTest"/>
/// to opt in to this capability.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IReproductionExport
{
    /// <summary>Whether to export a reproduction file when the test fails.</summary>
    bool ExportReproductionOnFailure { get; }

    /// <summary>Output path for exported reproduction files.</summary>
    string ReproductionOutputPath { get; }
}
