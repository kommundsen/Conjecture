namespace Conjecture.Core.Internal.Shrinker;

internal sealed class ShrinkState
{
    private IReadOnlyList<IRNode> _nodes;
    private readonly Func<IReadOnlyList<IRNode>, Status> _isInteresting;

    internal IReadOnlyList<IRNode> Nodes => _nodes;

    internal ShrinkState(IReadOnlyList<IRNode> nodes, Func<IReadOnlyList<IRNode>, Status> isInteresting)
    {
        _nodes = nodes;
        _isInteresting = isInteresting;
    }

    /// <summary>
    /// Try replacing current nodes with <paramref name="candidate"/>.
    /// Updates state and returns true only if the candidate is still interesting.
    /// </summary>
    internal bool TryUpdate(IReadOnlyList<IRNode> candidate)
    {
        if (_isInteresting(candidate) == Status.Interesting)
        {
            _nodes = candidate;
            return true;
        }
        return false;
    }
}
