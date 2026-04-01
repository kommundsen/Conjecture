namespace Conjecture.Core.Internal;

internal interface IShrinkPass
{
    /// <summary>Attempt one reduction step. Returns true if progress was made.</summary>
    ValueTask<bool> TryReduce(ShrinkState state);
}
