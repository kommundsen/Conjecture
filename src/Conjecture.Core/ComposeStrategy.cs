// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class GeneratorContext(ConjectureData data) : IGeneratorContext
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
}

internal sealed class ComposeStrategy<T>(Func<IGeneratorContext, T> factory) : Strategy<T>
{
    private const int MaxAttempts = 200;

    internal override T Generate(ConjectureData data)
    {
        for (var i = 0; i < MaxAttempts; i++)
        {
            try { return factory(new GeneratorContext(data)); }
            catch (UnsatisfiedAssumptionException) { }
        }
        data.MarkInvalid();
        throw new UnsatisfiedAssumptionException();
    }
}