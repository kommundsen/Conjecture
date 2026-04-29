// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class GenerationContext(ConjectureData data) : IGenerationContext
{
    public T Generate<T>(Strategy<T> strategy) => strategy.Generate(data);

    // Does NOT call MarkInvalid — ComposeStrategy owns the retry budget.
    public void Assume(bool condition)
    {
        if (!condition)
        {
            throw new UnsatisfiedAssumptionException();
        }
    }

    public void Target(double observation, string label = "default") =>
        data.RecordObservation(label, observation);
}

internal sealed class ComposeStrategy<T>(Func<IGenerationContext, T> factory) : Strategy<T>
{
    private const int MaxAttempts = 200;

    internal override T Generate(ConjectureData data)
    {
        for (var i = 0; i < MaxAttempts; i++)
        {
            try { return factory(new GenerationContext(data)); }
            catch (UnsatisfiedAssumptionException) { }
        }
        data.MarkInvalid();
        throw new UnsatisfiedAssumptionException();
    }
}