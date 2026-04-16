// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text;

namespace Conjecture.LinqPad;

internal static class HtmlShrinkTrace
{
    internal static string Render<T>(IReadOnlyList<T> steps)
    {
        StringBuilder sb = new();
        sb.Append("<table><thead><th>Step</th><th>Value</th></thead>");
        for (int i = 0; i < steps.Count; i++)
        {
            sb.Append("<tr><td>");
            sb.Append(i);
            sb.Append("</td><td>");
            sb.Append(steps[i]?.ToString() ?? "");
            sb.Append("</td></tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }
}