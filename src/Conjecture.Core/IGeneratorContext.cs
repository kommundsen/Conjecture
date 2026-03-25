using Conjecture.Core.Generation;

namespace Conjecture.Core;

/// <summary>Provides imperative draw and assume operations within a <c>Strategies.Compose</c> factory.</summary>
public interface IGeneratorContext
{
    /// <summary>Draws the next value from <paramref name="strategy"/>.</summary>
    T Next<T>(Strategy<T> strategy);

    /// <summary>Rejects the current test case if <paramref name="condition"/> is false.</summary>
    void Assume(bool condition);
}
