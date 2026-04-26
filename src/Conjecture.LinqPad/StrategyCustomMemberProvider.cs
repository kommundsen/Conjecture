// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Reflection;

using Conjecture.Core;

using LINQPad;

namespace Conjecture.LinqPad;

/// <summary>Wraps a <see cref="Strategy{T}"/> and implements <see cref="ICustomMemberProvider"/> for LINQPad display.</summary>
/// <param name="strategy">The strategy to display.</param>
public class StrategyCustomMemberProvider<T>(Strategy<T> strategy) : ICustomMemberProvider
{
    private static readonly bool IsConvertible = typeof(IConvertible).IsAssignableFrom(typeof(T));

    // SvgHistogram.Render<T> has where T : IConvertible — unreachable from unconstrained T without reflection.
    // Cached per generic instantiation so GetValues() enumeration doesn't re-fetch the MethodInfo.
    private static readonly MethodInfo? SvgHistogramRender =
        IsConvertible
            ? typeof(SvgHistogram)
                .GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(typeof(T))
            : null;

    /// <inheritdoc/>
    public IEnumerable<string> GetNames()
    {
        yield return "Preview";
        yield return "Sample Table";
        if (IsConvertible)
        {
            yield return "Histogram";
        }
    }

    /// <inheritdoc/>
    public IEnumerable<Type> GetTypes()
    {
        yield return typeof(object);
        yield return typeof(object);
        if (IsConvertible)
        {
            yield return typeof(object);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<object?> GetValues()
    {
        yield return WrapHtml(HtmlPreview.Render(strategy));
        yield return WrapHtml(HtmlSampleTable.Render(strategy));
        if (IsConvertible)
        {
            string histogramHtml = (string)SvgHistogramRender!.Invoke(null, new object?[] { strategy, 1000, 20, null })!;
            yield return WrapHtml(histogramHtml);
        }
    }

    private static object WrapHtml(string html)
    {
        try
        {
            return Util.RawHtml(html);
        }
        catch (FileLoadException)
        {
            return new HtmlString(html);
        }
    }

    private sealed class HtmlString(string html)
    {
        public override string ToString()
        {
            return html;
        }
    }
}