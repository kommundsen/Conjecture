
namespace Conjecture.Core;

/// <summary>Provides imperative draw and assume operations within a <c>Generate.Compose</c> factory.</summary>
public interface IGeneratorContext
{
    /// <summary>Generates a value from <paramref name="strategy"/>.</summary>
    T Generate<T>(Strategy<T> strategy);

    /// <summary>Rejects the current test case if <paramref name="condition"/> is false.</summary>
    void Assume(bool condition);
}
