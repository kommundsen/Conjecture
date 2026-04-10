// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class VersionStringStrategy(int maxMajor, int maxMinor, int maxPatch) : Strategy<string>
{
    internal override string Generate(ConjectureData data)
    {
        string major = DrawComponent(data, maxMajor);
        string minor = DrawComponent(data, maxMinor);
        string patch = DrawComponent(data, maxPatch);
        return $"{major}.{minor}.{patch}";
    }

    private static string DrawComponent(ConjectureData data, int max)
    {
        // Fixed-length single-digit draw so NumericAwareShrinkPass sees StringChar nodes
        // and can minimize each component toward '0' independently.
        data.NextStringLength(1UL, 1UL);
        char digit = (char)data.NextStringChar('0', (ulong)('0' + max));
        return digit.ToString();
    }
}
