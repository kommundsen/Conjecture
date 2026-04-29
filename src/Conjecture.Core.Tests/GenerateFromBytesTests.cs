// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests;

public class GenerateFromBytesTests
{
    [Fact]
    public void FromBytes_ReturnsNonNullStrategy()
    {
        byte[] buffer = [0x01, 0x02, 0x03, 0x04];

        Strategy<int> strategy = Strategy.FromBytes<int>(buffer);

        Assert.NotNull(strategy);
    }

    [Fact]
    public void FromBytes_IsDeterministic_SameBufferProducesSameValue()
    {
        byte[] buffer = [0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04];

        Strategy<int> strategy = Strategy.FromBytes<int>(buffer);

        ConjectureData dataA = ConjectureData.FromBuffer(buffer);
        ConjectureData dataB = ConjectureData.FromBuffer(buffer);

        int first = strategy.Generate(dataA);
        int second = strategy.Generate(dataB);

        Assert.Equal(first, second);
    }

    [Fact]
    public void FromBytes_AcceptsImplicitByteArrayConversion()
    {
        byte[] buffer = [0x00, 0x00, 0x00, 0x2A];

        // Passes byte[] where ReadOnlySpan<byte> is expected — implicit C# conversion must compile.
        Strategy<int> strategy = Strategy.FromBytes<int>(buffer);
        ConjectureData data = ConjectureData.FromBuffer(buffer);

        int value = strategy.Generate(data);

        Assert.IsType<int>(value);
    }

    [Fact]
    public void FromBytes_AcceptsStackallocSpan()
    {
        ReadOnlySpan<byte> span = stackalloc byte[4] { 0x01, 0x02, 0x03, 0x04 };

        // stackalloc must be accepted — signature must be ReadOnlySpan<byte>.
        Strategy<int> strategy = Strategy.FromBytes<int>(span);

        ConjectureData data = ConjectureData.FromBuffer(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        int value = strategy.Generate(data);

        Assert.IsType<int>(value);
    }

    [Fact]
    public void FromBytes_Label_IsNonNullAndDescriptive()
    {
        byte[] buffer = [0x01, 0x02, 0x03, 0x04];

        Strategy<int> strategy = Strategy.FromBytes<int>(buffer);

        Assert.NotNull(strategy.Label);
        Assert.False(string.IsNullOrWhiteSpace(strategy.Label));
    }

    [Fact]
    public void FromBytes_IgnoresCallerData_ProducesSameValueForDifferentData()
    {
        byte[] strategyBuffer = [0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04];
        Strategy<int> strategy = Strategy.FromBytes<int>(strategyBuffer);

        ConjectureData dataA = ConjectureData.FromBuffer([0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88]);
        ConjectureData dataB = ConjectureData.FromBuffer([0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA, 0xF9, 0xF8]);

        int first = strategy.Generate(dataA);
        int second = strategy.Generate(dataB);

        Assert.Equal(first, second);
    }
}