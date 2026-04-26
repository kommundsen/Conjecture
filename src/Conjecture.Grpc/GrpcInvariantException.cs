// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

namespace Conjecture.Grpc;

/// <summary>Thrown when a gRPC response invariant assertion fails.</summary>
public sealed class GrpcInvariantException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GrpcInvariantException"/> class.</summary>
    public GrpcInvariantException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GrpcInvariantException"/> class.</summary>
    public GrpcInvariantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}