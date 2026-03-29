namespace Conjecture.Core.Internal.Shrinker;

internal sealed class ShrinkState
{
    private readonly Func<IReadOnlyList<IRNode>, Status> isInteresting;

    internal IReadOnlyList<IRNode> Nodes { get; private set; }

    internal ShrinkState(IReadOnlyList<IRNode> nodes, Func<IReadOnlyList<IRNode>, Status> isInteresting)
    {
        Nodes = nodes;
        this.isInteresting = isInteresting;
    }

    internal int ShrinkCount { get; private set; }

    /// <summary>
    /// Try replacing current nodes with <paramref name="candidate"/>.
    /// Updates state and returns true only if the candidate is still interesting.
    /// </summary>
    internal bool TryUpdate(IReadOnlyList<IRNode> candidate)
    {
        if (isInteresting(candidate) == Status.Interesting)
        {
            Nodes = candidate;
            ShrinkCount++;
            return true;
        }
        return false;
    }
}
