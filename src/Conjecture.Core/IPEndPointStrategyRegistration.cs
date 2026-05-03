// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net;

namespace Conjecture.Core;

internal static class IPEndPointStrategyRegistration
{
    internal static void Register()
    {
        GenerateForRegistry.Register(
            typeof(IPEndPoint),
            static () => new IPEndPointProvider(),
            Strategy.Compose<object?>(static ctx => ctx.Generate(Strategy.IPEndPoints())));
    }

    private sealed class IPEndPointProvider : IStrategyProvider<IPEndPoint>
    {
        public Strategy<IPEndPoint> Create() => Strategy.IPEndPoints();
    }
}