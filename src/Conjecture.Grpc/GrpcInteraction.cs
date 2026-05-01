// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Interactions;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Grpc;

/// <summary>Immutable record covering all four gRPC call modes.</summary>
public sealed record GrpcInteraction(
    string ResourceName,
    string FullMethodName,
    GrpcRpcMode Mode,
    IReadOnlyList<ReadOnlyMemory<byte>> RequestMessages,
    IReadOnlyDictionary<string, string> Metadata,
    TimeSpan? Deadline = null) : IInteraction;