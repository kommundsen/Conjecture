// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

internal static class UriStrategyRegistration
{
    internal static void Register()
    {
        GenerateForRegistry.Register(
            typeof(Uri),
            static () => new UriProvider(),
            Strategy.Compose<object?>(static ctx => ctx.Generate(Strategy.Uris())));
    }

    private sealed class UriProvider : IStrategyProvider<Uri>
    {
        public Strategy<Uri> Create() => Strategy.Uris();
    }
}