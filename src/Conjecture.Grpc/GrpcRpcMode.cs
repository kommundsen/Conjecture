// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Grpc;

/// <summary>The four gRPC call modes.</summary>
public enum GrpcRpcMode
{
    /// <summary>Single request, single response.</summary>
    Unary,

    /// <summary>Single request, stream of responses.</summary>
    ServerStream,

    /// <summary>Stream of requests, single response.</summary>
    ClientStream,

    /// <summary>Stream of requests, stream of responses.</summary>
    Bidi,
}