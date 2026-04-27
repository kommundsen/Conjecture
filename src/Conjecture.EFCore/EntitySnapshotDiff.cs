// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Conjecture.EFCore;

/// <summary>The difference between two <see cref="EntitySnapshot"/> instances.</summary>
public sealed record EntitySnapshotDiff(
    IReadOnlyDictionary<Type, int> CountDeltas,
    IReadOnlyDictionary<Type, IReadOnlyList<object>> AddedKeys,
    IReadOnlyDictionary<Type, IReadOnlyList<object>> RemovedKeys)
{
    /// <summary>Returns <see langword="true"/> when no counts changed and no keys were added or removed.</summary>
    public bool IsEmpty => CountDeltas.Values.All(static d => d == 0)
                           && AddedKeys.Count == 0
                           && RemovedKeys.Count == 0;

    /// <summary>Returns a human-readable summary of all changes.</summary>
    public string ToReport()
    {
        if (IsEmpty)
        {
            return "(no changes)";
        }

        StringBuilder sb = new();
        foreach ((Type t, int delta) in CountDeltas.Where(static kv => kv.Value != 0))
        {
            sb.Append(CultureInfo.InvariantCulture, $"{t.Name}: {(delta > 0 ? "+" : "")}{delta}");
            if (AddedKeys.TryGetValue(t, out IReadOnlyList<object>? added) && added.Count > 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $" (added [{string.Join(", ", added)}])");
            }

            if (RemovedKeys.TryGetValue(t, out IReadOnlyList<object>? removed) && removed.Count > 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $" (removed [{string.Join(", ", removed)}])");
            }

            sb.Append("; ");
        }

        return sb.ToString().TrimEnd(';', ' ');
    }
}