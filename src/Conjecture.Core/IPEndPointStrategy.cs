// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Net;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class IPEndPointStrategy(Strategy<IPAddress> addresses, Strategy<int> ports) : Strategy<IPEndPoint>
{
    internal override IPEndPoint Generate(ConjectureData data)
    {
        IPAddress address = addresses.Generate(data);
        int port = ports.Generate(data);
        return port is < 0 or > 65535
            ? throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be in the range [0, 65535].")
            : new IPEndPoint(address, port);
    }
}