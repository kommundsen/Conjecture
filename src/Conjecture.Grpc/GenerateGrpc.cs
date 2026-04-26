// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Core;

using Grpc.Core;

namespace Conjecture.Grpc;

/// <summary>Strategy factory methods for gRPC interactions.</summary>
public static class GenerateGrpc
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(0);

    /// <summary>
    /// Returns a <see cref="Strategy{T}"/> that generates a unary <see cref="GrpcInteraction"/>.
    /// </summary>
    public static Strategy<GrpcInteraction> Unary<TReq, TResp>(
        string resourceName,
        Method<TReq, TResp> method,
        Strategy<TReq> requestStrategy,
        IReadOnlyDictionary<string, string>? metadata = null)
        where TReq : class
        where TResp : class
    {
        IReadOnlyDictionary<string, string> resolvedMetadata = metadata ?? EmptyMetadata;
        return requestStrategy.Select(req =>
        {
            ReadOnlyMemory<byte> bytes = method.RequestMarshaller.Serializer(req);
            return new GrpcInteraction(
                resourceName,
                method.FullName,
                GrpcRpcMode.Unary,
                [bytes],
                resolvedMetadata);
        });
    }
}