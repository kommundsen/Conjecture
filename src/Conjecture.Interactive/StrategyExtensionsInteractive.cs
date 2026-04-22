// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Interactive;

/// <summary>Extension methods for rendering Strategy samples as text for interactive exploration.</summary>
public static class StrategyExtensionsInteractive
{
    private const int PreviewMaxCount = 100;
    private const int SampleTableMaxCount = 50;

    extension<T>(Strategy<T> strategy)
    {
        /// <summary>Renders up to <paramref name="count"/> sampled values as a comma-separated string.</summary>
        public string Preview(int count = 20, ulong? seed = null)
        {
            bool capped = count > PreviewMaxCount;
            int effective = capped ? PreviewMaxCount : count;
            IReadOnlyList<T> samples = DataGen.Sample(strategy, effective, seed);

            StringBuilder sb = new();
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
                sb.AppendLine();
                sb.Append("(Showing ");
                sb.Append(PreviewMaxCount);
                sb.Append(" values, capped from ");
                sb.Append(count);
                sb.Append(')');
            }

            return sb.ToString();
        }

        /// <summary>Renders up to <paramref name="count"/> sampled values in a two-column text table.</summary>
        public string SampleTable(int count = 10, ulong? seed = null)
        {
            bool capped = count > SampleTableMaxCount;
            int effective = capped ? SampleTableMaxCount : count;
            IReadOnlyList<T> samples = DataGen.Sample(strategy, effective, seed);

            // Compute column widths.
            int indexWidth = Math.Max(1, samples.Count.ToString().Length);
            int valueWidth = 5; // minimum "Value" header length
            string[] values = new string[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                values[i] = samples[i]?.ToString() ?? "";
                if (values[i].Length > valueWidth)
                {
                    valueWidth = values[i].Length;
                }
            }

            StringBuilder sb = new();
            sb.Append(new string(' ', indexWidth));
            sb.Append(" # │ Value");
            sb.AppendLine();
            sb.Append(new string('─', indexWidth + 2));
            sb.Append('┼');
            sb.Append(new string('─', valueWidth + 2));
            for (int i = 0; i < samples.Count; i++)
            {
                sb.AppendLine();
                sb.Append(i.ToString().PadLeft(indexWidth + 2));
                sb.Append(" │ ");
                sb.Append(values[i]);
            }

            if (capped)
            {
                sb.AppendLine();
                sb.Append("(Showing ");
                sb.Append(SampleTableMaxCount);
                sb.Append(" values, capped from ");
                sb.Append(count);
                sb.Append(')');
            }

            return sb.ToString();
        }

        /// <summary>Runs a shrink trace for <paramref name="strategy"/> starting from <paramref name="seed"/>, recording each accepted reduction.</summary>
        public ShrinkTraceResult<T> ShrinkTrace(ulong seed, Func<T, bool> failingProperty)
        {
            SplittableRandom rng = new(seed);
            ConjectureData initialData = ConjectureData.ForGeneration(rng.Split());
            T initialValue = strategy.Generate(initialData);

            if (!failingProperty(initialValue))
            {
                throw new ArgumentException("Property must fail on the generated value to produce a shrink trace.");
            }

            List<ShrinkStep<T>> steps = [new(initialValue)];

            ValueTask<Status> IsInteresting(IReadOnlyList<IRNode> nodes)
            {
                try
                {
                    ConjectureData data = ConjectureData.ForRecord(nodes);
                    T value = strategy.Generate(data);
                    if (data.Status == Status.Overrun)
                    {
                        return ValueTask.FromResult(Status.Overrun);
                    }

                    if (failingProperty(value))
                    {
                        steps.Add(new(value));
                        return ValueTask.FromResult(Status.Interesting);
                    }

                    return ValueTask.FromResult(Status.Valid);
                }
                catch (UnsatisfiedAssumptionException)
                {
                    return ValueTask.FromResult(Status.Invalid);
                }
                catch (InvalidOperationException)
                {
                    return ValueTask.FromResult(Status.Overrun);
                }
            }

            Shrinker.ShrinkAsync(initialData.IRNodes, IsInteresting).GetAwaiter().GetResult();

            // Compute column widths.
            int stepWidth = Math.Max(4, steps.Count.ToString().Length); // "Step" header
            int valueWidth = 5; // "Value" header
            string[] rendered = new string[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                rendered[i] = steps[i].Value?.ToString() ?? "";
                if (rendered[i].Length > valueWidth)
                {
                    valueWidth = rendered[i].Length;
                }
            }

            StringBuilder sb = new();
            sb.Append("Step".PadLeft(stepWidth));
            sb.Append(" │ Value");
            sb.AppendLine();
            sb.Append(new string('─', stepWidth));
            sb.Append('┼');
            sb.Append(new string('─', valueWidth + 2));
            for (int i = 0; i < steps.Count; i++)
            {
                sb.AppendLine();
                sb.Append(i.ToString().PadLeft(stepWidth));
                sb.Append(" │ ");
                sb.Append(rendered[i]);
            }

            return new ShrinkTraceResult<T>(steps, sb.ToString());
        }

        /// <summary>Renders a text histogram of sampled values projected by <paramref name="selector"/>.</summary>
        public string Histogram(Func<T, double> selector, int sampleSize = 1000, int bucketCount = 20, ulong? seed = null)
        {
            IReadOnlyList<T> samples = DataGen.Sample(strategy, sampleSize, seed);
            List<double> doubles = new(samples.Count);
            foreach (T value in samples)
            {
                doubles.Add(selector(value));
            }

            return TextHistogram.Render(doubles, bucketCount);
        }
    }

#pragma warning disable RS0026 // multiple overloads with optional parameters
    extension<T>(Strategy<T> strategy) where T : IConvertible
    {
        /// <summary>Renders a text histogram of sampled values from <paramref name="strategy"/>.</summary>
        public string Histogram(int sampleSize = 1000, int bucketCount = 20, ulong? seed = null)
        {
            IReadOnlyList<T> samples = DataGen.Sample(strategy, sampleSize, seed);
            List<double> doubles = new(samples.Count);
            foreach (T value in samples)
            {
                doubles.Add(Convert.ToDouble(value));
            }

            return TextHistogram.Render(doubles, bucketCount);
        }
    }
#pragma warning restore RS0026
}
