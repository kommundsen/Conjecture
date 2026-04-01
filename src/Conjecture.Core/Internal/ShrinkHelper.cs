// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal static class ShrinkHelper
{
    internal static IRNode[] Replace(IReadOnlyList<IRNode> nodes, int index, IRNode replacement)
    {
        IRNode[] arr = new IRNode[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            arr[i] = i == index ? replacement : nodes[i];
        }

        return arr;
    }

    internal static IRNode[] Without(IReadOnlyList<IRNode> nodes, int index)
    {
        IRNode[] arr = new IRNode[nodes.Count - 1];
        int dst = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i != index)
            {
                arr[dst++] = nodes[i];
            }
        }

        return arr;
    }

    internal static IRNode[] WithoutInterval(IReadOnlyList<IRNode> nodes, int start, int length)
    {
        IRNode[] arr = new IRNode[nodes.Count - length];
        int dst = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i < start || i >= start + length)
            {
                arr[dst++] = nodes[i];
            }
        }
        return arr;
    }
}