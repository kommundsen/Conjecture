namespace Conjecture.Core.Internal.Shrinker;

internal interface IShrinkPass
{
    /// <summary>Attempt one reduction step. Returns true if progress was made.</summary>
    bool TryReduce(ShrinkState state);
}
