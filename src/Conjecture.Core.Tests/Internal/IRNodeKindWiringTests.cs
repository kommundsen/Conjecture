// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public sealed class IRNodeKindWiringTests : IDisposable
{
    private readonly string tempDir;
    private readonly ExampleDatabase db;

    public IRNodeKindWiringTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        db = new ExampleDatabase(Path.Combine(tempDir, "test.db"));
    }

    public void Dispose()
    {
        db.Dispose();
        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // ── Strategy wiring ──────────────────────────────────────────────────────

    [Fact]
    public void FloatingPointStrategy_Double_Bounded_RecordsFloat64Nodes()
    {
        FloatingPointStrategy<double> strategy = new(0.0, 1.0);
        ConjectureData data = MakeData();

        strategy.Generate(data);

        Assert.Contains(data.IRNodes, n => n.Kind == IRNodeKind.Float64);
    }

    [Fact]
    public void FloatingPointStrategy_Double_Bounded_NoIntegerNodes()
    {
        FloatingPointStrategy<double> strategy = new(0.0, 1.0);
        ConjectureData data = MakeData();

        strategy.Generate(data);

        Assert.DoesNotContain(data.IRNodes, n => n.Kind == IRNodeKind.Integer);
    }

    [Fact]
    public void FloatingPointStrategy_Float_Bounded_RecordsFloat32Nodes()
    {
        FloatingPointStrategy<float> strategy = new(0f, 1f);
        ConjectureData data = MakeData();

        strategy.Generate(data);

        Assert.Contains(data.IRNodes, n => n.Kind == IRNodeKind.Float32);
    }

    [Fact]
    public void FloatingPointStrategy_Float_Bounded_NoIntegerNodes()
    {
        FloatingPointStrategy<float> strategy = new(0f, 1f);
        ConjectureData data = MakeData();

        strategy.Generate(data);

        Assert.DoesNotContain(data.IRNodes, n => n.Kind == IRNodeKind.Integer);
    }

    [Fact]
    public void StringStrategy_RecordsStringLengthNode()
    {
        StringStrategy strategy = new(minLength: 1, maxLength: 5);
        ConjectureData data = MakeData();

        strategy.Generate(data);

        Assert.Contains(data.IRNodes, n => n.Kind == IRNodeKind.StringLength);
    }

    [Fact]
    public void StringStrategy_RecordsExactlyOneLengthNode()
    {
        StringStrategy strategy = new(minLength: 2, maxLength: 5);
        ConjectureData data = MakeData();

        strategy.Generate(data);

        Assert.Equal(1, data.IRNodes.Count(n => n.Kind == IRNodeKind.StringLength));
    }

    [Fact]
    public void StringStrategy_CharNodeCountMatchesStringLength()
    {
        StringStrategy strategy = new(minLength: 2, maxLength: 5);
        ConjectureData data = MakeData();

        string result = strategy.Generate(data);

        Assert.Equal(result.Length, data.IRNodes.Count(n => n.Kind == IRNodeKind.StringChar));
    }

    [Fact]
    public void StringStrategy_NoIntegerNodes()
    {
        StringStrategy strategy = new(minLength: 1, maxLength: 5);
        ConjectureData data = MakeData();

        strategy.Generate(data);

        Assert.DoesNotContain(data.IRNodes, n => n.Kind == IRNodeKind.Integer);
    }

    // ── Serialization round-trips ─────────────────────────────────────────────

    [Fact]
    public async Task Serialization_Float64Node_RoundTripsViaDatabase()
    {
        const string testId = "float64-roundtrip";
        ConjectureSettings settings = new() { MaxExamples = 1, Database = true };
        FloatingPointStrategy<double> strategy = new(0.0, 1.0);

        await TestRunner.Run(settings, data =>
        {
            strategy.Generate(data);
            throw new InvalidOperationException("fail");
        }, db, testId);

        IReadOnlyList<IRNode>? replayNodes = null;
        await TestRunner.Run(settings, data =>
        {
            strategy.Generate(data);
            if (data.IsReplay)
            {
                replayNodes = data.IRNodes;
            }
            throw new InvalidOperationException("fail");
        }, db, testId);

        Assert.NotNull(replayNodes);
        Assert.Contains(replayNodes, n => n.Kind == IRNodeKind.Float64);
    }

    [Fact]
    public async Task Serialization_Float32Node_RoundTripsViaDatabase()
    {
        const string testId = "float32-roundtrip";
        ConjectureSettings settings = new() { MaxExamples = 1, Database = true };
        FloatingPointStrategy<float> strategy = new(0f, 1f);

        await TestRunner.Run(settings, data =>
        {
            strategy.Generate(data);
            throw new InvalidOperationException("fail");
        }, db, testId);

        IReadOnlyList<IRNode>? replayNodes = null;
        await TestRunner.Run(settings, data =>
        {
            strategy.Generate(data);
            if (data.IsReplay)
            {
                replayNodes = data.IRNodes;
            }
            throw new InvalidOperationException("fail");
        }, db, testId);

        Assert.NotNull(replayNodes);
        Assert.Contains(replayNodes, n => n.Kind == IRNodeKind.Float32);
    }

    [Fact]
    public async Task Serialization_StringNodes_RoundTripsViaDatabase()
    {
        const string testId = "string-roundtrip";
        ConjectureSettings settings = new() { MaxExamples = 1, Database = true };
        StringStrategy strategy = new(minLength: 1, maxLength: 5);

        await TestRunner.Run(settings, data =>
        {
            strategy.Generate(data);
            throw new InvalidOperationException("fail");
        }, db, testId);

        IReadOnlyList<IRNode>? replayNodes = null;
        await TestRunner.Run(settings, data =>
        {
            strategy.Generate(data);
            if (data.IsReplay)
            {
                replayNodes = data.IRNodes;
            }
            throw new InvalidOperationException("fail");
        }, db, testId);

        Assert.NotNull(replayNodes);
        Assert.Contains(replayNodes, n => n.Kind == IRNodeKind.StringLength);
    }

    // ── Backward compatibility ────────────────────────────────────────────────

    [Fact]
    public async Task Deserialization_OldIntegerKindBytes_DeserializesCorrectly()
    {
        const string testId = "legacy-integer";
        const ulong expectedValue = 7UL;
        const ulong min = 0UL;
        const ulong max = 10UL;

        db.Save(testId, BuildIntegerNodeBuffer(expectedValue, min, max));

        ulong? replayed = null;
        ConjectureSettings settings = new() { MaxExamples = 1, Database = true };
        await TestRunner.Run(settings, data =>
        {
            if (data.IsReplay)
            {
                replayed = data.NextInteger(min, max);
            }
        }, db, testId);

        Assert.Equal(expectedValue, replayed);
    }

    private static byte[] BuildIntegerNodeBuffer(ulong value, ulong min, ulong max)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);
        writer.Write(1);           // node count
        writer.Write((byte)0);     // kind = Integer (0x00)
        writer.Write(value);
        writer.Write(min);
        writer.Write(max);
        return ms.ToArray();
    }
}