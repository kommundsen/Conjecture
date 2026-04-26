// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Interactions;

using Grpc.Core;

namespace Conjecture.Grpc;

/// <summary>gRPC-specific interaction target exposing the underlying call invoker.</summary>
public interface IGrpcTarget : IInteractionTarget
{
    /// <summary>Returns the <see cref="CallInvoker"/> for the given resource name.</summary>
    CallInvoker GetCallInvoker(string resourceName);
}