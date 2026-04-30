// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;

using Conjecture.Core;

using Grpc.Core;

namespace Conjecture.Grpc;

/// <summary>Extension methods for generating gRPC interactions.</summary>
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
            Build(resourceName, method, GrpcRpcMode.Unary, [req], resolvedMetadata));
    }

    /// <summary>
    /// Returns a <see cref="Strategy{T}"/> that generates a server-streaming <see cref="GrpcInteraction"/>.
    /// </summary>
    public static Strategy<GrpcInteraction> ServerStream<TReq, TResp>(
        string resourceName,
        Method<TReq, TResp> method,
        Strategy<TReq> requestStrategy,
        IReadOnlyDictionary<string, string>? metadata = null)
        where TReq : class
        where TResp : class
    {
        IReadOnlyDictionary<string, string> resolvedMetadata = metadata ?? EmptyMetadata;
        return requestStrategy.Select(req =>
            Build(resourceName, method, GrpcRpcMode.ServerStream, [req], resolvedMetadata));
    }

    /// <summary>
    /// Returns a <see cref="Strategy{T}"/> that generates a client-streaming <see cref="GrpcInteraction"/>.
    /// </summary>
    public static Strategy<GrpcInteraction> ClientStream<TReq, TResp>(
        string resourceName,
        Method<TReq, TResp> method,
        Strategy<IReadOnlyList<TReq>> requestsStrategy,
        IReadOnlyDictionary<string, string>? metadata = null)
        where TReq : class
        where TResp : class
    {
        IReadOnlyDictionary<string, string> resolvedMetadata = metadata ?? EmptyMetadata;
        return requestsStrategy.Select(reqs =>
            Build(resourceName, method, GrpcRpcMode.ClientStream, reqs, resolvedMetadata));
    }

    /// <summary>
    /// Returns a <see cref="Strategy{T}"/> that generates a bidirectional-streaming <see cref="GrpcInteraction"/>.
    /// </summary>
    public static Strategy<GrpcInteraction> BidiStream<TReq, TResp>(
        string resourceName,
        Method<TReq, TResp> method,
        Strategy<IReadOnlyList<TReq>> requestsStrategy,
        IReadOnlyDictionary<string, string>? metadata = null)
        where TReq : class
        where TResp : class
    {
        IReadOnlyDictionary<string, string> resolvedMetadata = metadata ?? EmptyMetadata;
        return requestsStrategy.Select(reqs =>
            Build(resourceName, method, GrpcRpcMode.Bidi, reqs, resolvedMetadata));
    }

    private static GrpcInteraction Build<TReq, TResp>(
        string resourceName,
        Method<TReq, TResp> method,
        GrpcRpcMode mode,
        IReadOnlyList<TReq> requests,
        IReadOnlyDictionary<string, string> metadata)
        where TReq : class
        where TResp : class
    {
        System.ReadOnlyMemory<byte>[] messages = requests
            .Select(req => (System.ReadOnlyMemory<byte>)method.RequestMarshaller.Serializer(req))
            .ToArray();
        return new GrpcInteraction(resourceName, method.FullName, mode, messages, metadata);
    }
}