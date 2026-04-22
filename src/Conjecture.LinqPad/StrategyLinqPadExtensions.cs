// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Interactive;

using LINQPad;

namespace Conjecture.LinqPad;

/// <summary>LINQPad extension methods for shrink trace visualisation.</summary>
public static class StrategyLinqPadExtensions
{
    extension<T>(Strategy<T> strategy)
    {
        /// <summary>Runs a shrink trace and returns an HTML table as a LINQPad raw-HTML object.</summary>
        public object ShrinkTraceHtml(int seed, Func<T, bool> failingProperty)
        {
            ShrinkTraceResult<T> result = strategy.ShrinkTrace(SeedHelpers.ToUlong(seed), failingProperty);
            List<T> values = new(result.Steps.Count);
            foreach (ShrinkStep<T> step in result.Steps)
            {
                values.Add(step.Value);
            }

            string html = HtmlShrinkTrace.Render(values);
            return Util.RawHtml(html);
        }
    }
}
