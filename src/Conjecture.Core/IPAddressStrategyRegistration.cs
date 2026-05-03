// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net;

namespace Conjecture.Core;

internal static class IPAddressStrategyRegistration
{
    internal static void Register()
    {
        GenerateForRegistry.Register(
            typeof(IPAddress),
            static () => new IPAddressProvider(),
            Strategy.Compose<object?>(static ctx => ctx.Generate(Strategy.IPAddresses())));
    }

    private sealed class IPAddressProvider : IStrategyProvider<IPAddress>
    {
        public Strategy<IPAddress> Create() => Strategy.IPAddresses();
    }
}