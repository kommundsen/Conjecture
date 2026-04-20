// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class CharStrategy : Strategy<char>
{
    internal override char Generate(ConjectureData data)
    {
        ulong raw = data.NextInteger(0UL, char.MaxValue);
        return (char)raw;
    }
}
