// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class IdentifierStrategy(
    int minPrefixLength,
    int maxPrefixLength,
    int minDigits,
    int maxDigits) : Strategy<string>
{
    internal override string Generate(ConjectureData data)
    {
        int prefixLen = (int)data.NextStringLength((ulong)minPrefixLength, (ulong)maxPrefixLength);
        StringBuilder sb = new();
        for (int i = 0; i < prefixLen; i++)
        {
            sb.Append((char)data.NextStringChar('a', 'z'));
        }
        int digitLen = (int)data.NextStringLength((ulong)minDigits, (ulong)maxDigits);
        for (int i = 0; i < digitLen; i++)
        {
            sb.Append((char)data.NextStringChar('0', '9'));
        }
        return sb.ToString();
    }
}