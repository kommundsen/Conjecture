namespace Conjecture.Core.Internal.Shrinker;

internal sealed class ShrinkState
{
    private IReadOnlyList<IRNode> nodes;
    private readonly Func<IReadOnlyList<IRNode>, Status> isInteresting;

    internal IReadOnlyList<IRNode> Nodes => nodes;

    internal ShrinkState(IReadOnlyList<IRNode> nodes, Func<IReadOnlyList<IRNode>, Status> isInteresting)
    {
        this.nodes = nodes;
        this.isInteresting = isInteresting;
    }

    /// <summary>
    /// Try replacing current nodes with <paramref name="candidate"/>.
    /// Updates state and returns true only if the candidate is still interesting.
    /// </summary>
    internal bool TryUpdate(IReadOnlyList<IRNode> candidate)
    {
        if (isInteresting(candidate) == Status.Interesting)
        {
            nodes = candidate;
            return true;
        }
        return false;
    }
}
