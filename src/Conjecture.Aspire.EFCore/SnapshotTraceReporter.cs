// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text;

using Conjecture.Abstractions.Aspire;

namespace Conjecture.Aspire.EFCore;

/// <summary>Records snapshot observations for failure trace reports.</summary>
public sealed class SnapshotTraceReporter
{
    private readonly List<RecordedSnapshot> snapshots = [];

    /// <summary>Records a snapshot observation into the trace.</summary>
    public void RecordSnapshot(object snapshot, object? capturedValue)
    {
        string label = snapshot is ISnapshotLabel labeled ? labeled.Label : snapshot.GetType().Name;
        snapshots.Add(new(label, capturedValue));
    }

    /// <summary>Formats the accumulated snapshot observations as a human-readable string.</summary>
    public string FormatReport()
    {
        if (snapshots.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        sb.AppendLine("=== DB snapshots ===");
        foreach (RecordedSnapshot snap in snapshots)
        {
            sb.AppendLine($"  [{snap.Label}] = {snap.Value}");
        }

        return sb.ToString();
    }

    private readonly record struct RecordedSnapshot(string Label, object? Value);
}
