// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics;

namespace Conjecture.Core.Internal;

internal static class TestRunner
{
    private readonly record struct DbContext(ExampleDatabase Database, string TestIdHash);

    internal static Task<TestRunResult> Run(
        ConjectureSettings settings,
        Action<ConjectureData> test,
        ExampleDatabase db,
        string testIdHash)
    {
        return RunCore(settings, WrapSync(test), db, testIdHash);
    }

    internal static Task<TestRunResult> Run(ConjectureSettings settings, Action<ConjectureData> test)
    {
        return RunGenerationCore(settings, WrapSync(test), null);
    }

    internal static Task<TestRunResult> RunAsync(
        ConjectureSettings settings,
        Func<ConjectureData, Task> test,
        ExampleDatabase db,
        string testIdHash)
    {
        return RunCore(settings, test, db, testIdHash);
    }

    internal static Task<TestRunResult> RunAsync(
        ConjectureSettings settings,
        Func<ConjectureData, Task> test)
    {
        return RunGenerationCore(settings, test, null);
    }

    private static async Task<TestRunResult> RunCore(
        ConjectureSettings settings,
        Func<ConjectureData, Task> test,
        ExampleDatabase db,
        string testIdHash)
    {
        bool useDb = settings.UseDatabase && settings.Seed is null;

        if (useDb)
        {
            IReadOnlyList<byte[]> stored = db.Load(testIdHash);
            foreach (byte[] buffer in stored)
            {
                List<IRNode> nodes = DeserializeNodes(buffer);
                Status replayStatus = await ReplayAsync(nodes, test);
                if (replayStatus == Status.Interesting)
                {
                    (IReadOnlyList<IRNode> shrunk, int shrinkCount) = await Shrinker.ShrinkAsync(
                        nodes, n => ReplayAsync(n, test));
                    db.Delete(testIdHash);
                    db.Save(testIdHash, SerializeNodes(shrunk));
                    return TestRunResult.Fail(shrunk, nodes, 0UL, 1, shrinkCount);
                }
            }

            if (stored.Count > 0)
            {
                db.Delete(testIdHash);
            }
        }

        DbContext? dbContext = useDb ? new(db, testIdHash) : null;
        return await RunGenerationCore(settings, test, dbContext);
    }

    private static async Task<TestRunResult> RunGenerationCore(
        ConjectureSettings settings,
        Func<ConjectureData, Task> test,
        DbContext? dbContext)
    {
        ulong seed = settings.Seed ?? (ulong)Random.Shared.NextInt64();
        SplittableRandom rng = new(seed);
        int valid = 0;
        int unsatisfied = 0;
        int totalAttempts = 0;
        int maxAttempts = settings.MaxExamples * 200;
        TimeSpan? deadline = settings.Deadline;
        Stopwatch? sw = deadline.HasValue ? Stopwatch.StartNew() : null;

        while (valid < settings.MaxExamples && totalAttempts < maxAttempts)
        {
            totalAttempts++;
            ConjectureData data = ConjectureData.ForGeneration(rng.Split());
            Target.CurrentData.Value = data;
            try
            {
                Task testTask = test(data);
                if (deadline.HasValue)
                {
                    await testTask.WaitAsync(deadline.Value);
                }
                else
                {
                    await testTask;
                }

                valid++;
                if (sw is not null && sw.Elapsed > deadline!.Value)
                {
                    throw new ConjectureException("deadline exceeded");
                }
            }
            catch (TimeoutException)
            {
                throw new ConjectureException("deadline exceeded");
            }
            catch (UnsatisfiedAssumptionException)
            {
                data.MarkInvalid();
                unsatisfied++;
                if ((valid > 0 || settings.MaxUnsatisfiedRatio == 0) && unsatisfied > valid * settings.MaxUnsatisfiedRatio)
                {
                    throw new ConjectureException("too many unsatisfied assumptions");
                }
            }
            catch (ConjectureException)
            {
                throw;
            }
            catch (Exception failureEx)
            {
                data.MarkInteresting();
                string? stackTrace = failureEx.StackTrace;
                IReadOnlyList<IRNode> firstFailureNodes = data.IRNodes;
                (IReadOnlyList<IRNode> shrunk, int shrinkCount) = await Shrinker.ShrinkAsync(
                    firstFailureNodes, n => ReplayAsync(n, test));
                if (dbContext is DbContext ctx)
                {
                    ctx.Database.Save(ctx.TestIdHash, SerializeNodes(shrunk));
                }

                return TestRunResult.Fail(shrunk, firstFailureNodes, seed, valid + 1, shrinkCount, stackTrace);
            }
            finally
            {
                Target.CurrentData.Value = null;
                data.Freeze();
            }
        }

        return TestRunResult.Pass(seed, valid);
    }

    private static Func<ConjectureData, Task> WrapSync(Action<ConjectureData> test)
    {
        return data => { test(data); return Task.CompletedTask; };
    }

    private static async ValueTask<Status> ReplayAsync(IReadOnlyList<IRNode> nodes, Func<ConjectureData, Task> test)
    {
        ConjectureData data = ConjectureData.ForRecord(nodes);
        try
        {
            await test(data);
            return Status.Valid;
        }
        catch (UnsatisfiedAssumptionException)
        {
            return Status.Invalid;
        }
        catch (InvalidOperationException) when (data.Status == Status.Overrun)
        {
            return Status.Overrun;
        }
        catch (Exception)
        {
            return Status.Interesting;
        }
    }

    private static byte[] SerializeNodes(IReadOnlyList<IRNode> nodes)
    {
        // 4 (count) + per node: 1 (kind) + 8 (value) + 8 (min) + 8 (max) = 25 bytes; Bytes nodes add 4 + rawLen
        int estimatedSize = 4 + nodes.Count * 25;
        using MemoryStream ms = new(estimatedSize);
        using BinaryWriter writer = new(ms);
        writer.Write(nodes.Count);
        foreach (IRNode node in nodes)
        {
            writer.Write((byte)node.Kind);
            writer.Write(node.Value);
            writer.Write(node.Min);
            writer.Write(node.Max);
            if (node.Kind == IRNodeKind.Bytes)
            {
                byte[] raw = node.RawBytes ?? new byte[(int)node.Value];
                writer.Write(raw.Length);
                writer.Write(raw);
            }
        }
        return ms.ToArray();
    }

    private static List<IRNode> DeserializeNodes(byte[] buffer)
    {
        try
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms);
            int count = reader.ReadInt32();
            List<IRNode> nodes = new(count);
            for (int i = 0; i < count; i++)
            {
                IRNodeKind kind = (IRNodeKind)reader.ReadByte();
                ulong value = reader.ReadUInt64();
                ulong min = reader.ReadUInt64();
                ulong max = reader.ReadUInt64();
                byte[]? rawBytes = null;
                int rawLen = 0;
                if (kind == IRNodeKind.Bytes)
                {
                    rawLen = reader.ReadInt32();
                    rawBytes = reader.ReadBytes(rawLen);
                }

                nodes.Add(kind switch
                {
                    IRNodeKind.Bytes => IRNode.ForBytes(rawLen, rawBytes),
                    IRNodeKind.Boolean => IRNode.ForBoolean(value == 1UL),
                    IRNodeKind.Float64 => IRNode.ForFloat64(value, min, max),
                    IRNodeKind.Float32 => IRNode.ForFloat32(value, min, max),
                    IRNodeKind.StringLength => IRNode.ForStringLength(value, min, max),
                    IRNodeKind.StringChar => IRNode.ForStringChar(value, min, max),
                    _ => IRNode.ForInteger(value, min, max),
                });
            }
            return nodes;
        }
        catch (Exception)
        {
            // Corrupt or unrecognised buffer — treat as no stored example
            return [];
        }
    }
}