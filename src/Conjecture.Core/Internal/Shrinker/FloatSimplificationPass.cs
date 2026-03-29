using System.Runtime.CompilerServices;

namespace Conjecture.Core.Internal.Shrinker;

internal sealed class FloatSimplificationPass : IShrinkPass
{
    private static readonly ulong Zero64 = Unsafe.BitCast<double, ulong>(0.0);
    private static readonly ulong MaxFinite64 = Unsafe.BitCast<double, ulong>(double.MaxValue);
    private static readonly ulong MinFinite64 = Unsafe.BitCast<double, ulong>(double.MinValue);
    private const ulong SignBit64 = 0x8000000000000000UL;

    private static readonly ulong Zero32 = Unsafe.BitCast<float, uint>(0f);
    private static readonly ulong MaxFinite32 = Unsafe.BitCast<float, uint>(float.MaxValue);
    private static readonly ulong MinFinite32 = Unsafe.BitCast<float, uint>(float.MinValue);
    private const ulong SignBit32 = 0x80000000UL;

    public bool TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            IRNode node = state.Nodes[i];
            if (node.Kind == IRNodeKind.Float64 && TryReduceAt64(state, i, node))
            {
                return true;
            }
            if (node.Kind == IRNodeKind.Float32 && TryReduceAt32(state, i, node))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryReduceAt64(ShrinkState state, int index, IRNode node)
    {
        ulong bits = node.Value;
        foreach (ulong candidate in Candidates64(bits))
        {
            IRNode[] arr = [..state.Nodes];
            arr[index] = IRNode.ForFloat64(candidate, node.Min, node.Max);
            if (state.TryUpdate(arr))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryReduceAt32(ShrinkState state, int index, IRNode node)
    {
        ulong bits = node.Value;
        foreach (ulong candidate in Candidates32(bits))
        {
            IRNode[] arr = [..state.Nodes];
            arr[index] = IRNode.ForFloat32(candidate, node.Min, node.Max);
            if (state.TryUpdate(arr))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<ulong> Candidates64(ulong bits)
    {
        if (bits == Zero64)
        {
            yield break;
        }
        yield return Zero64;
        double d = Unsafe.BitCast<ulong, double>(bits);
        if (double.IsNaN(d))
        {
            yield break;
        }
        if ((bits & SignBit64) != 0)
        {
            ulong positive64 = bits & ~SignBit64;
            if (positive64 != Zero64)
            {
                yield return positive64;
            }
        }
        if (double.IsInfinity(d))
        {
            yield return (bits & SignBit64) != 0 ? MinFinite64 : MaxFinite64;
        }
        else
        {
            ulong halved = Unsafe.BitCast<double, ulong>(d / 2.0);
            if (halved != bits && halved != Zero64)
            {
                yield return halved;
            }
        }
    }

    private static IEnumerable<ulong> Candidates32(ulong bits)
    {
        if (bits == Zero32)
        {
            yield break;
        }
        yield return Zero32;
        float f = Unsafe.BitCast<uint, float>((uint)bits);
        if (float.IsNaN(f))
        {
            yield break;
        }
        if ((bits & SignBit32) != 0)
        {
            ulong positive32 = bits & ~SignBit32;
            if (positive32 != Zero32)
            {
                yield return positive32;
            }
        }
        if (float.IsInfinity(f))
        {
            yield return (bits & SignBit32) != 0 ? MinFinite32 : MaxFinite32;
        }
        else
        {
            ulong halved = Unsafe.BitCast<float, uint>(f / 2f);
            if (halved != bits && halved != Zero32)
            {
                yield return halved;
            }
        }
    }
}
