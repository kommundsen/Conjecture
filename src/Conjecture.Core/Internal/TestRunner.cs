using System.Diagnostics;

using Conjecture.Core.Internal.Database;
using ShrinkEngine = Conjecture.Core.Internal.Shrinker.Shrinker;

namespace Conjecture.Core.Internal;

internal static class TestRunner
{
    private readonly record struct DbContext(ExampleDatabase Database, string TestIdHash);

    internal static TestRunResult Run(
        ConjectureSettings settings,
        Action<ConjectureData> test,
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
                Status replayStatus = Replay(nodes, test);
                if (replayStatus == Status.Interesting)
                {
                    var (shrunk, shrinkCount) = ShrinkEngine.Shrink(nodes, n => Replay(n, test));
                    db.Delete(testIdHash);
                    db.Save(testIdHash, SerializeNodes(shrunk));
                    return TestRunResult.Fail(shrunk, 0UL, 1, shrinkCount);
                }
            }

            if (stored.Count > 0)
            {
                db.Delete(testIdHash);
            }
        }

        DbContext? dbContext = useDb ? new(db, testIdHash) : null;
        return RunGeneration(settings, test, dbContext);
    }

    internal static TestRunResult Run(ConjectureSettings settings, Action<ConjectureData> test)
    {
        return RunGeneration(settings, test, null);
    }

    private static TestRunResult RunGeneration(
        ConjectureSettings settings,
        Action<ConjectureData> test,
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
            try
            {
                test(data);
                valid++;
                if (sw is not null && sw.Elapsed > deadline!.Value)
                {
                    throw new ConjectureException("deadline exceeded");
                }
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
            catch
            {
                data.MarkInteresting();
                var (shrunk, shrinkCount) = ShrinkEngine.Shrink(data.IRNodes, nodes => Replay(nodes, test));
                if (dbContext is DbContext ctx)
                {
                    ctx.Database.Save(ctx.TestIdHash, SerializeNodes(shrunk));
                }
                return TestRunResult.Fail(shrunk, seed, valid + 1, shrinkCount);
            }
            finally
            {
                data.Freeze();
            }
        }

        return TestRunResult.Pass(seed, valid);
    }

    private static Status Replay(IReadOnlyList<IRNode> nodes, Action<ConjectureData> test)
    {
        ConjectureData data = ConjectureData.ForRecord(nodes);
        try
        {
            test(data);
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
        catch
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
                if (kind == IRNodeKind.Bytes)
                {
                    int rawLen = reader.ReadInt32();
                    byte[] rawBytes = reader.ReadBytes(rawLen);
                    nodes.Add(IRNode.ForBytes(rawLen, rawBytes));
                }
                else if (kind == IRNodeKind.Boolean)
                {
                    nodes.Add(IRNode.ForBoolean(value == 1UL));
                }
                else
                {
                    nodes.Add(IRNode.ForInteger(value, min, max));
                }
            }
            return nodes;
        }
        catch
        {
            // Corrupt or unrecognised buffer — treat as no stored example
            return [];
        }
    }
}
