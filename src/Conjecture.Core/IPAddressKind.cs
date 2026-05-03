// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Specifies which IP address families <see cref="Strategy.IPAddresses"/> should generate.</summary>
[Flags]
public enum IPAddressKind
{
    /// <summary>Generate only IPv4 addresses (<see cref="System.Net.Sockets.AddressFamily.InterNetwork"/>).</summary>
    V4 = 1,

    /// <summary>Generate only IPv6 addresses (<see cref="System.Net.Sockets.AddressFamily.InterNetworkV6"/>).</summary>
    V6 = 2,

    /// <summary>Generate both IPv4 and IPv6 addresses.</summary>
    Both = V4 | V6,
}