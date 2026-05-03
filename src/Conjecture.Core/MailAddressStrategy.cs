// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net.Mail;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class EmailAddressStringStrategy : Strategy<string>
{
    private readonly IdentifierStrategy localPart = new(1, 6, 1, 4);
    private readonly HostStrategy hostPart = new(2, 2);

    internal override string Generate(ConjectureData data)
    {
        string local = localPart.Generate(data);
        string host = hostPart.Generate(data);
        return local + "@" + host;
    }
}

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