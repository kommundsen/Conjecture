// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Aspire;

/// <summary>Marker interface for interactions that carry a human-readable snapshot label.</summary>
internal interface ISnapshotLabel
{
    /// <summary>Human-readable label for the snapshot observation.</summary>
    string Label { get; }
}
