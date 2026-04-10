// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class NumericStringStrategy(
    int minDigits,
    int maxDigits,
    string? prefix = null,
    string? suffix = null) : Strategy<string>
{
    internal override string Generate(ConjectureData data)
    {
        int len = (int)data.NextStringLength((ulong)minDigits, (ulong)maxDigits);
        StringBuilder sb = new();
        if (prefix is not null)
        {
            sb.Append(prefix);
        }
        for (int i = 0; i < len; i++)
        {
            sb.Append((char)data.NextStringChar('0', '9'));
        }
        if (suffix is not null)
        {
            sb.Append(suffix);
        }
        return sb.ToString();
    }
}