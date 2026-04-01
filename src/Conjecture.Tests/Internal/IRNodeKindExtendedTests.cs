// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class IRNodeKindExtendedTests
{
    [Fact]
    public void IRNodeKind_Float64_HasValue3()
    {
        Assert.Equal(3, (int)IRNodeKind.Float64);
    }

    [Fact]
    public void IRNodeKind_Float32_HasValue4()
    {
        Assert.Equal(4, (int)IRNodeKind.Float32);
    }

    [Fact]
    public void IRNodeKind_StringLength_HasValue5()
    {
        Assert.Equal(5, (int)IRNodeKind.StringLength);
    }

    [Fact]
    public void IRNodeKind_StringChar_HasValue6()
    {
        Assert.Equal(6, (int)IRNodeKind.StringChar);
    }

    [Fact]
    public void IRNodeKind_ExistingKinds_Unchanged()
    {
        Assert.Equal(0, (int)IRNodeKind.Integer);
        Assert.Equal(1, (int)IRNodeKind.Boolean);
        Assert.Equal(2, (int)IRNodeKind.Bytes);
    }

    [Fact]
    public void ForFloat64_SetsKindValueMinMax()
    {
        IRNode node = IRNode.ForFloat64(42UL, 0UL, 100UL);

        Assert.Equal(IRNodeKind.Float64, node.Kind);
        Assert.Equal(42UL, node.Value);
        Assert.Equal(0UL, node.Min);
        Assert.Equal(100UL, node.Max);
    }

    [Fact]
    public void ForFloat32_SetsKindValueMinMax()
    {
        IRNode node = IRNode.ForFloat32(7UL, 1UL, 50UL);

        Assert.Equal(IRNodeKind.Float32, node.Kind);
        Assert.Equal(7UL, node.Value);
        Assert.Equal(1UL, node.Min);
        Assert.Equal(50UL, node.Max);
    }

    [Fact]
    public void ForStringLength_SetsKindValueMinMax()
    {
        IRNode node = IRNode.ForStringLength(10UL, 0UL, 64UL);

        Assert.Equal(IRNodeKind.StringLength, node.Kind);
        Assert.Equal(10UL, node.Value);
        Assert.Equal(0UL, node.Min);
        Assert.Equal(64UL, node.Max);
    }

    [Fact]
    public void ForStringChar_SetsKindValueMinMax()
    {
        IRNode node = IRNode.ForStringChar(97UL, 32UL, 126UL);

        Assert.Equal(IRNodeKind.StringChar, node.Kind);
        Assert.Equal(97UL, node.Value);
        Assert.Equal(32UL, node.Min);
        Assert.Equal(126UL, node.Max);
    }
}