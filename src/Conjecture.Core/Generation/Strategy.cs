using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

/// <summary>Base class for all Conjecture strategies that generate values of type <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The type of value produced by this strategy.</typeparam>
public abstract class Strategy<T>
{
    internal abstract T Next(ConjectureData data);
}
