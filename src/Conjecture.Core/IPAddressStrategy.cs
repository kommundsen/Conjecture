// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class IPAddressStrategy(IPAddressKind kind) : Strategy<IPAddress>
{
    internal override IPAddress Generate(ConjectureData data)
    {
        bool generateV4 = kind switch
        {
            IPAddressKind.V4 => true,
            IPAddressKind.V6 => false,
            _ => data.NextBoolean(),
        };

        return generateV4 ? GenerateAddress(data, 4) : GenerateAddress(data, 16);
    }

    private static IPAddress GenerateAddress(ConjectureData data, int byteCount)
    {
        Span<byte> bytes = stackalloc byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            bytes[i] = (byte)data.NextInteger(0UL, 255UL);
        }

        return new IPAddress(bytes);
    }
}