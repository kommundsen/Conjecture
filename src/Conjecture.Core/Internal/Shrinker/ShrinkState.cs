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

    /// <summary>Index of the node most recently modified by a successful TryUpdate call, or -1.</summary>
    internal int LastModifiedIndex { get; private set; } = -1;

    /// <summary>
    /// Try replacing current nodes with <paramref name="candidate"/>.
    /// Updates state and returns true only if the candidate is still interesting.
    /// Resets <see cref="LastModifiedIndex"/> to -1 on success.
    /// </summary>
    internal bool TryUpdate(IReadOnlyList<IRNode> candidate)
    {
        return TryUpdate(candidate, -1);
    }

    /// <summary>
    /// Try replacing current nodes with <paramref name="candidate"/>, recording
    /// <paramref name="modifiedIndex"/> as <see cref="LastModifiedIndex"/> on success.
    /// </summary>
    internal bool TryUpdate(IReadOnlyList<IRNode> candidate, int modifiedIndex)
    {
        if (isInteresting(candidate) == Status.Interesting)
        {
            Nodes = candidate;
            ShrinkCount++;
            LastModifiedIndex = modifiedIndex;
            return true;
        }
        return false;
    }
}
