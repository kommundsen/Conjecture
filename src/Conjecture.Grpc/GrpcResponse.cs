// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Grpc.Core;

namespace Conjecture.Grpc;

/// <summary>Uniform result shape returned from gRPC target execution.</summary>
public sealed record GrpcResponse(
    StatusCode Status,
    string? StatusDetail,
    IReadOnlyList<ReadOnlyMemory<byte>> ResponseMessages,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    IReadOnlyDictionary<string, string> Trailers);