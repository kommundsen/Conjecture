// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text;

using Conjecture.Core;

namespace Conjecture.LinqPad;

internal static class HtmlSampleTable
{
    private const int MaxRows = 50;

    internal static string Render<T>(Strategy<T> strategy, int count = 10, int? seed = null)
    {
        bool capped = count > MaxRows;
        int effective = capped ? MaxRows : count;
        ulong? ulongSeed = SeedHelpers.ToUlong(seed);
        IReadOnlyList<T> samples = DataGen.Sample(strategy, effective, ulongSeed);

        StringBuilder sb = new();
        sb.Append("<table><thead><th>#</th><th>Value</th></thead>");
        for (int i = 0; i < samples.Count; i++)
        {
            sb.Append("<tr><td>");
            sb.Append(i + 1);
            sb.Append("</td><td>");
            sb.Append(samples[i]?.ToString() ?? "");
            sb.Append("</td></tr>");
        }

        sb.Append("</table>");
        if (capped)
        {
            sb.Append("<p>truncated (showing ");
            sb.Append(MaxRows);
            sb.Append(" of ");
            sb.Append(count);
            sb.Append(")</p>");
        }
        return sb.ToString();
    }
}