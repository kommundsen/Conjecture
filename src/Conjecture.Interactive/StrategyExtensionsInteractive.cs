// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using Conjecture.Core;

namespace Conjecture.Interactive;

/// <summary>Extension methods for rendering Strategy samples as HTML in interactive notebooks.</summary>
public static class StrategyExtensionsInteractive
{
    private const int PreviewMaxCount = 100;
    private const int SampleTableMaxCount = 50;

    /// <summary>Renders up to <paramref name="count"/> sampled values in a single-row HTML table.</summary>
    public static string Preview<T>(this Strategy<T> strategy, int count = 20, ulong? seed = null)
    {
        bool capped = count > PreviewMaxCount;
        int effective = capped ? PreviewMaxCount : count;
        IReadOnlyList<T> samples = DataGen.Sample(strategy, effective, seed);

        StringBuilder sb = new();
        sb.Append("<table><tr>");
        foreach (T value in samples)
        {
            sb.Append("<td>");
            sb.Append(WebUtility.HtmlEncode(value?.ToString() ?? string.Empty));
            sb.Append("</td>");
        }

        sb.Append("</tr></table>");

        if (capped)
        {
            sb.Append("<p>Showing ");
            sb.Append(PreviewMaxCount);
            sb.Append(" values (capped from ");
            sb.Append(count);
            sb.Append(").</p>");
        }

        return sb.ToString();
    }

    /// <summary>Renders up to <paramref name="count"/> sampled values in a two-column index/value HTML table.</summary>
    public static string SampleTable<T>(this Strategy<T> strategy, int count = 10, ulong? seed = null)
    {
        bool capped = count > SampleTableMaxCount;
        int effective = capped ? SampleTableMaxCount : count;
        IReadOnlyList<T> samples = DataGen.Sample(strategy, effective, seed);

        StringBuilder sb = new();
        sb.Append("<table><thead><tr><th>Index</th><th>Value</th></tr></thead><tbody>");
        for (int i = 0; i < samples.Count; i++)
        {
            sb.Append("<tr><th scope=\"row\">");
            sb.Append(i);
            sb.Append("</th><td>");
            sb.Append(WebUtility.HtmlEncode(samples[i]?.ToString() ?? string.Empty));
            sb.Append("</td></tr>");
        }

        sb.Append("</tbody></table>");

        if (capped)
        {
            sb.Append("<p>Showing ");
            sb.Append(SampleTableMaxCount);
            sb.Append(" values (capped from ");
            sb.Append(count);
            sb.Append(").</p>");
        }

        return sb.ToString();
    }

#pragma warning disable RS0026 // multiple overloads with optional parameters
    /// <summary>Renders a histogram SVG of sampled values from <paramref name="strategy"/>.</summary>
    public static string Histogram<T>(this Strategy<T> strategy, int sampleSize = 1000, int bucketCount = 20, ulong? seed = null)
        where T : IConvertible
    {
        IReadOnlyList<T> samples = DataGen.Sample(strategy, sampleSize, seed);
        List<double> doubles = new(samples.Count);
        foreach (T value in samples)
        {
            doubles.Add(Convert.ToDouble(value));
        }

        return SvgHistogram.Render(doubles, bucketCount);
    }

    /// <summary>Renders a histogram SVG of sampled values projected by <paramref name="selector"/>.</summary>
    public static string Histogram<T>(this Strategy<T> strategy, Func<T, double> selector, int sampleSize = 1000, int bucketCount = 20, ulong? seed = null)
    {
        IReadOnlyList<T> samples = DataGen.Sample(strategy, sampleSize, seed);
        List<double> doubles = new(samples.Count);
        foreach (T value in samples)
        {
            doubles.Add(selector(value));
        }

        return SvgHistogram.Render(doubles, bucketCount);
    }
#pragma warning restore RS0026
}