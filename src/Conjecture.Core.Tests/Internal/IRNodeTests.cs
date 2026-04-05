// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class IRNodeTests
{
    [Fact]
    public void IRNodeKind_HasExpectedValues()
    {
        _ = IRNodeKind.Integer;
        _ = IRNodeKind.Boolean;
        _ = IRNodeKind.Bytes;
    }

    [Fact]
    public void ForInteger_SetsKindAndValue()
    {
        var node = IRNode.ForInteger(7UL, 0UL, 10UL);

        Assert.Equal(IRNodeKind.Integer, node.Kind);
        Assert.Equal(7UL, node.Value);
        Assert.Equal(0UL, node.Min);
        Assert.Equal(10UL, node.Max);
    }

    [Fact]
    public void ForBoolean_True_SetsValueOne()
    {
        var node = IRNode.ForBoolean(true);

        Assert.Equal(IRNodeKind.Boolean, node.Kind);
        Assert.Equal(1UL, node.Value);
    }

    [Fact]
    public void ForBoolean_False_SetsValueZero()
    {
        var node = IRNode.ForBoolean(false);

        Assert.Equal(IRNodeKind.Boolean, node.Kind);
        Assert.Equal(0UL, node.Value);
    }

    [Fact]
    public void ForBytes_SetsKindAndLength()
    {
        var node = IRNode.ForBytes(16);

        Assert.Equal(IRNodeKind.Bytes, node.Kind);
        Assert.Equal(16UL, node.Value);
    }

    [Fact]
    public void ForInteger_RoundTrip()
    {
        var node = IRNode.ForInteger(42UL, 0UL, 100UL);

        Assert.Equal(42UL, node.Value);
        Assert.Equal(0UL, node.Min);
        Assert.Equal(100UL, node.Max);
    }
}