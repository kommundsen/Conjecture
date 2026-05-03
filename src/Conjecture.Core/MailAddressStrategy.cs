// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net.Mail;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class MailAddressStrategy : Strategy<MailAddress>
{
    private readonly EmailAddressStringStrategy stringStrategy = new();

    internal override MailAddress Generate(ConjectureData data)
    {
        string address = stringStrategy.Generate(data);
        try
        {
            return new MailAddress(address);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"EmailAddressStringStrategy produced an invalid address: '{address}'", ex);
        }
    }
}