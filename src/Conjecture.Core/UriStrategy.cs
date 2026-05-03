// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class UriStrategy : Strategy<Uri>
{
    private readonly UriKind kind;
    private readonly Strategy<string> schemeStrategy;
    private readonly HostStrategy dnsHostStrategy;
    private readonly IPAddressStrategy ipHostStrategy;
    private readonly IdentifierStrategy pathSegmentStrategy;

    internal UriStrategy(UriKind uriKind, IReadOnlyList<string> schemes)
    {
        kind = uriKind;
        schemeStrategy = Strategy.SampledFrom(schemes);
        dnsHostStrategy = new(1, 3);
        ipHostStrategy = new(IPAddressKind.Both);
        pathSegmentStrategy = new(1, 6, 1, 4);
    }

    internal override Uri Generate(ConjectureData data)
    {
        bool absolute = kind switch
        {
            UriKind.Absolute => true,
            UriKind.Relative => false,
            _ => data.NextBoolean(),
        };

        string uriString = absolute ? BuildAbsolute(data) : BuildRelative(data);

        UriKind parseKind = absolute ? UriKind.Absolute : UriKind.Relative;
        return Uri.TryCreate(uriString, parseKind, out Uri? uri)
            ? uri
            : throw new InvalidOperationException($"UriStrategy produced an invalid URI string: '{uriString}'");
    }

    private string BuildAbsolute(ConjectureData data)
    {
        string scheme = schemeStrategy.Generate(data);
        string host = BuildHost(data);
        StringBuilder sb = new();
        sb.Append(scheme);
        sb.Append("://");
        sb.Append(host);
        AppendOptionalPort(data, sb);
        AppendPath(data, sb);
        return sb.ToString();
    }

    private string BuildRelative(ConjectureData data)
    {
        StringBuilder sb = new();
        AppendPath(data, sb);
        return sb.Length > 0 ? sb.ToString() : "/";
    }

    private string BuildHost(ConjectureData data)
    {
        bool useIp = data.NextBoolean();
        if (!useIp)
        {
            return dnsHostStrategy.Generate(data);
        }

        IPAddress addr = ipHostStrategy.Generate(data);
        return addr.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{addr}]"
            : addr.ToString();
    }

    private static void AppendOptionalPort(ConjectureData data, StringBuilder sb)
    {
        if (data.NextBoolean())
        {
            int port = (int)data.NextInteger(1UL, 65535UL);
            sb.Append(':');
            sb.Append(port);
        }
    }

    private void AppendPath(ConjectureData data, StringBuilder sb)
    {
        int segmentCount = (int)data.NextInteger(0UL, 3UL);
        for (int i = 0; i < segmentCount; i++)
        {
            sb.Append('/');
            sb.Append(pathSegmentStrategy.Generate(data));
        }

        if (segmentCount == 0)
        {
            sb.Append('/');
        }
    }
}