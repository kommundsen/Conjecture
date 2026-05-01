// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Grpc;

using GrpcCore = global::Grpc.Core;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Grpc.Tests;

public class IGrpcTargetTests
{
    [Fact]
    public void IGrpcTarget_ExtendsIInteractionTarget()
    {
        Assert.True(typeof(IInteractionTarget).IsAssignableFrom(typeof(IGrpcTarget)));
    }

    [Fact]
    public void IGrpcTarget_DeclaresGetCallInvokerMethod()
    {
        System.Reflection.MethodInfo? method = typeof(IGrpcTarget).GetMethod("GetCallInvoker");

        Assert.NotNull(method);
        Assert.Equal(typeof(GrpcCore.CallInvoker), method!.ReturnType);
    }
}