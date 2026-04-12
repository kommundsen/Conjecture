// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class ConjectureDataBufferReplayTests
{
    [Fact]
    public void NextBytes_FirstCall_ReturnsExpectedSlice()
    {
        byte[] buffer = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        ConjectureData data = ConjectureData.FromBuffer(buffer);

        byte[] result = data.NextBytes(4);

        Assert.Equal([0x01, 0x02, 0x03, 0x04], result);
    }

    [Fact]
    public void NextBytes_SecondCall_ReturnsNextSlice()
    {
        byte[] buffer = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        ConjectureData data = ConjectureData.FromBuffer(buffer);

        data.NextBytes(4);
        byte[] second = data.NextBytes(4);

        Assert.Equal([0x05, 0x06, 0x07, 0x08], second);
    }

    [Fact]
    public void FromBuffer_TwoInstances_ProduceSameSequence()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];

        ConjectureData first = ConjectureData.FromBuffer(buffer);
        ConjectureData second = ConjectureData.FromBuffer(buffer);

        byte[] firstA = first.NextBytes(3);
        byte[] firstB = first.NextBytes(3);

        byte[] secondA = second.NextBytes(3);
        byte[] secondB = second.NextBytes(3);

        Assert.Equal(firstA, secondA);
        Assert.Equal(firstB, secondB);
    }

    [Fact]
    public void NextBytes_AfterBufferExhausted_ReturnsZeros()
    {
        byte[] buffer = [0x01, 0x02];
        ConjectureData data = ConjectureData.FromBuffer(buffer);

        data.NextBytes(2); // consumes all bytes

        byte[] overflow = data.NextBytes(4);

        Assert.Equal([0x00, 0x00, 0x00, 0x00], overflow);
    }

    [Fact]
    public void NextBytes_PartiallyExhausted_PadsWithZeros()
    {
        byte[] buffer = [0x11, 0x22, 0x33];
        ConjectureData data = ConjectureData.FromBuffer(buffer);

        data.NextBytes(2); // consumes 0x11, 0x22

        byte[] result = data.NextBytes(4); // only 0x33 remains

        Assert.Equal([0x33, 0x00, 0x00, 0x00], result);
    }

    [Fact]
    public void NextBytes_EmptyBuffer_AlwaysReturnsZeros()
    {
        ConjectureData data = ConjectureData.FromBuffer([]);

        byte[] result = data.NextBytes(3);

        Assert.Equal([0x00, 0x00, 0x00], result);
    }

    [Fact]
    public void Status_RemainsValid_AfterBufferExhausted()
    {
        ConjectureData data = ConjectureData.FromBuffer([0x01]);

        data.NextBytes(1);
        data.NextBytes(4); // beyond buffer

        Assert.Equal(Status.Valid, data.Status);
    }

    [Fact]
    public void IRNodes_RecordsEachNextBytesCall()
    {
        byte[] buffer = [0x01, 0x02, 0x03, 0x04];
        ConjectureData data = ConjectureData.FromBuffer(buffer);

        data.NextBytes(2);
        data.NextBytes(2);

        Assert.Equal(2, data.IRNodes.Count);
        Assert.All(data.IRNodes, n => Assert.Equal(IRNodeKind.Bytes, n.Kind));
    }

    [Fact]
    public void NextInteger_KnownBuffer_ReturnsValueDerivedFromBytes()
    {
        // Buffer encodes raw ulong 6 (little-endian).
        // PrngAdapter.NextUInt64(rng, max=9): threshold = (ulong.MaxValue - 9) % 10 = 6.
        // x=6 >= threshold=6, so result = 6 % 10 = 6; final = 6 + min(0) = 6.
        byte[] buffer = [0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        ConjectureData data = ConjectureData.FromBuffer(buffer);

        ulong result = data.NextInteger(min: 0, max: 9);

        Assert.Equal(6UL, result);
    }

    [Fact]
    public void FromBuffer_SecondInstanceFromSameArray_StartsFromByteZeroRegardlessOfFirstInstanceCursor()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11, 0x22];
        ConjectureData first = ConjectureData.FromBuffer(buffer);

        first.NextBytes(6); // advance first instance's cursor

        ConjectureData second = ConjectureData.FromBuffer(buffer);
        byte[] secondResult = second.NextBytes(4);

        Assert.Equal([0xAA, 0xBB, 0xCC, 0xDD], secondResult);
    }
}