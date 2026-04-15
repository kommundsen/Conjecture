// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text;

using Conjecture.Core;

namespace Conjecture.LinqPad;

internal static class HtmlPreview
{
    private const int MaxCount = 100;

    internal static string Render<T>(Strategy<T> strategy, int count = 20, int? seed = null)
    {
        bool capped = count > MaxCount;
        int effective = capped ? MaxCount : count;
        ulong? ulongSeed = SeedHelpers.ToUlong(seed);
        IReadOnlyList<T> samples = DataGen.Sample(strategy, effective, ulongSeed);

        StringBuilder sb = new();
        sb.Append("<span>");
        for (int i = 0; i < samples.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(samples[i]?.ToString() ?? "");
        }

        if (capped)
        {
            sb.Append(", ... (truncated)");
        }

        sb.Append("</span>");
        return sb.ToString();
    }
}