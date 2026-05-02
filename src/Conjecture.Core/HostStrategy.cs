// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class HostStrategy(int minLabels, int maxLabels) : Strategy<string>
{
    internal override string Generate(ConjectureData data)
    {
        int labelCount = (int)data.NextStringLength((ulong)minLabels, (ulong)maxLabels);
        StringBuilder sb = new();

        for (int i = 0; i < labelCount - 1; i++)
        {
            if (i > 0)
            {
                sb.Append('.');
            }

            AppendAlphanumericLabel(data, sb);
        }

        if (labelCount > 1)
        {
            sb.Append('.');
        }

        AppendTldLabel(data, sb);

        return sb.ToString();
    }

    private static void AppendAlphanumericLabel(ConjectureData data, StringBuilder sb)
    {
        int len = (int)data.NextStringLength(1UL, 6UL);
        for (int i = 0; i < len; i++)
        {
            bool isDigit = i > 0 && data.NextBoolean();
            sb.Append(isDigit
                ? (char)data.NextStringChar('0', '9')
                : (char)data.NextStringChar('a', 'z'));
        }
    }

    private static void AppendTldLabel(ConjectureData data, StringBuilder sb)
    {
        int len = (int)data.NextStringLength(2UL, 6UL);
        for (int i = 0; i < len; i++)
        {
            sb.Append((char)data.NextStringChar('a', 'z'));
        }
    }
}