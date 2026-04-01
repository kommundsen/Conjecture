using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class GeneratorContext : IGeneratorContext
{
    private readonly ConjectureData data;
    internal GeneratorContext(ConjectureData data) => this.data = data;

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

internal sealed class ComposeStrategy<T> : Strategy<T>
{
    private const int MaxAttempts = 200;
    private readonly Func<IGeneratorContext, T> factory;
    internal ComposeStrategy(Func<IGeneratorContext, T> factory) => this.factory = factory;

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
