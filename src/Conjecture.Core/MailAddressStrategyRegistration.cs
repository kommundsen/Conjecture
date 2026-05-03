// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net.Mail;

namespace Conjecture.Core;

internal static class MailAddressStrategyRegistration
{
    internal static void Register()
    {
        GenerateForRegistry.Register(
            typeof(MailAddress),
            static () => new MailAddressProvider(),
            Strategy.Compose<object?>(static ctx => ctx.Generate(Strategy.EmailAddresses())));
    }

    private sealed class MailAddressProvider : IStrategyProvider<MailAddress>
    {
        public Strategy<MailAddress> Create() => Strategy.EmailAddresses();
    }
}