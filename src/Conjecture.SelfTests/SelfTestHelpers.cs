using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.SelfTests;

internal static class SelfTestHelpers
{
    internal static Status Replay(IReadOnlyList<IRNode> nodes, Action<ConjectureData> predicate)
    {
        ConjectureData data = ConjectureData.ForRecord(nodes);
        try
        {
            predicate(data);
            return Status.Valid;
        }
        catch (UnsatisfiedAssumptionException)
        {
            return Status.Invalid;
        }
        catch (InvalidOperationException) when (data.Status == Status.Overrun)
        {
            return Status.Overrun;
        }
        catch
        {
            return Status.Interesting;
        }
    }

    internal static bool IsLexicographicallyLeq(IReadOnlyList<IRNode> a, IReadOnlyList<IRNode> b)
    {
        int minLen = Math.Min(a.Count, b.Count);
        for (int i = 0; i < minLen; i++)
        {
            if (a[i].Value < b[i].Value) return true;
            if (a[i].Value > b[i].Value) return false;
        }
        return a.Count <= b.Count;
    }
}
