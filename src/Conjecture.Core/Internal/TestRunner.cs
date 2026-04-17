// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Diagnostics;

using Microsoft.Extensions.Logging;

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
                Instruments.DatabaseReplaysTotal.Add(1);
                Status replayStatus = await ReplayAsync(nodes, test);
                if (replayStatus == Status.Interesting)
                {
                    Instruments.ExamplesTotal.Add(1);
                    Instruments.FailuresTotal.Add(1);
                    (IReadOnlyList<IRNode> shrunk, int shrinkCount) = await Shrinker.ShrinkAsync(
                        nodes, n => ReplayAsync(n, test), settings.Logger);
                    db.Delete(testIdHash);
                    db.Save(testIdHash, SerializeNodes(shrunk));
                    Instruments.DatabaseSavesTotal.Add(1);
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
        Activity? rootActivity = ConjectureObservability.ActivitySource.StartActivity("PropertyTest");
        string seedTag = settings.Seed.HasValue ? $"0x{settings.Seed.Value:X16}" : "random";
        rootActivity?.SetTag("conjecture.seed", seedTag);
        rootActivity?.SetTag("conjecture.max_examples", settings.MaxExamples);
        if (settings.TestName is not null)
        {
            rootActivity?.SetTag("test.name", settings.TestName);
        }

        if (settings.TestClassName is not null)
        {
            rootActivity?.SetTag("test.class.name", settings.TestClassName);
        }

        ILogger logger = settings.Logger;
        ulong seed = settings.Seed ?? (ulong)Random.Shared.NextInt64();
        SplittableRandom rng = new(seed);
        int valid = 0;
        int unsatisfied = 0;
        int totalAttempts = 0;
        bool unsatisfiedWarnLogged = false;
        Stopwatch generationSw = Stopwatch.StartNew();
        int generationBudget = settings.Targeting
            ? Math.Max(1, (int)(settings.MaxExamples * (1.0 - settings.TargetingProportion)))
            : settings.MaxExamples;
        int maxAttempts = settings.MaxExamples * 200;
        TimeSpan? deadline = settings.Deadline;
        Stopwatch? sw = deadline.HasValue ? Stopwatch.StartNew() : null;
        Dictionary<string, (IReadOnlyList<IRNode> Nodes, double Score)> bestPerLabel = [];

        Activity? generationActivity = ConjectureObservability.ActivitySource.StartActivity("PropertyTest.Generation");

        try
        {
            while (totalAttempts < maxAttempts)
            {
                // Stop when we've reached the generation budget AND have observations to target,
                // or when we've exhausted the full MaxExamples budget.
                bool atGenerationBudget = valid >= generationBudget && bestPerLabel.Count > 0;
                if (valid >= settings.MaxExamples || atGenerationBudget)
                {
                    break;
                }

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
                    Instruments.ExamplesTotal.Add(1);
                    foreach ((string label, double score) in data.Observations)
                    {
                        if (!bestPerLabel.TryGetValue(label, out (IReadOnlyList<IRNode> Nodes, double Score) current) || score > current.Score)
                        {
                            bestPerLabel[label] = (data.IRNodes, score);
                        }
                    }

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
                    Instruments.RejectionsTotal.Add(1);
                    if (!unsatisfiedWarnLogged && valid > 0 && unsatisfied > valid * settings.MaxUnsatisfiedRatio / 2)
                    {
                        unsatisfiedWarnLogged = true;
                        Log.HighUnsatisfiedRatio(logger, unsatisfied, valid, settings.MaxUnsatisfiedRatio);
                    }

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
                    Instruments.ExamplesTotal.Add(1);
                    Instruments.FailuresTotal.Add(1);
                    string? stackTrace = failureEx.StackTrace;
                    IReadOnlyList<IRNode> firstFailureNodes = data.IRNodes;
                    generationActivity?.SetTag("examples", valid + 1);
                    generationActivity?.SetTag("failures", 1);
                    generationActivity?.SetTag("rejections", unsatisfied);
                    generationActivity?.Stop();

                    Activity? shrinkActivity = ConjectureObservability.ActivitySource.StartActivity("PropertyTest.Shrinking");
                    (IReadOnlyList<IRNode> shrunk, int shrinkCount) = await Shrinker.ShrinkAsync(
                        firstFailureNodes, n => ReplayAsync(n, test), logger);
                    shrinkActivity?.SetTag("reductions", shrinkCount);
                    shrinkActivity?.Stop();

                    if (dbContext is DbContext ctx)
                    {
                        ctx.Database.Save(ctx.TestIdHash, SerializeNodes(shrunk));
                        Instruments.DatabaseSavesTotal.Add(1);
                    }

                    generationSw.Stop();
                    Instruments.DurationSeconds.Record(generationSw.Elapsed.TotalSeconds);
                    Log.PropertyTestFailure(logger, valid + 1, $"0x{seed:X16}");
                    rootActivity?.SetTag("test.status", "fail");
                    rootActivity?.Stop();
                    return TestRunResult.Fail(shrunk, firstFailureNodes, seed, valid + 1, shrinkCount, stackTrace);
                }
                finally
                {
                    Target.CurrentData.Value = null;
                    data.Freeze();
                }
            }

            generationActivity?.SetTag("examples", valid);
            generationActivity?.SetTag("failures", 0);
            generationActivity?.SetTag("rejections", unsatisfied);
            generationActivity?.Stop();

            generationSw.Stop();
            Instruments.DurationSeconds.Record(generationSw.Elapsed.TotalSeconds);
            Log.GenerationCompleted(logger, valid, unsatisfied, generationSw.Elapsed.TotalMilliseconds);

            // Targeting phase: round-robin HillClimber across labels, one step per label per cycle.
            if (settings.Targeting && bestPerLabel.Count > 0)
            {
                Activity? targetingActivity = ConjectureObservability.ActivitySource.StartActivity("PropertyTest.Targeting");

                try
                {
                    int budgetRemaining = settings.MaxExamples - generationBudget;
                    SplittableRandom perturbRng = new(rng.NextUInt64());
                    List<string> labels = bestPerLabel.Keys.ToList();
                    string targetingLabels = string.Join(", ", labels);
                    Log.TargetingStarted(logger, targetingLabels);
                    Dictionary<string, IReadOnlyList<IRNode>> currentNodes = bestPerLabel.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Nodes);
                    Dictionary<string, double> currentScores = bestPerLabel.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Score);
                    IReadOnlyList<IRNode>? failingNodes = null;

                    async Task<(Status, IReadOnlyDictionary<string, double>)> EvalForClimb(IReadOnlyList<IRNode> nodes)
                    {
                        (Status status, IReadOnlyDictionary<string, double> obs) = await ReplayAndObserveAsync(nodes, test);
                        if (status == Status.Interesting)
                        {
                            failingNodes = nodes;
                        }

                        return (status, obs);
                    }

                    int labelIdx = 0;
                    while (budgetRemaining > 0 && failingNodes is null)
                    {
                        string label = labels[labelIdx++ % labels.Count];

                        // budget: 2 so the greedy pass can try both value+1 and value-1 per step,
                        // enabling both maximization and minimization (via negated scores) to make
                        // progress with a single label visit.
                        int stepBudget = Math.Min(2, budgetRemaining);
                        (IReadOnlyList<IRNode> newNodes, double newScore) = await HillClimber.Climb(
                            currentNodes[label], currentScores[label], label, EvalForClimb, stepBudget, perturbRng, logger);

                        currentNodes[label] = newNodes;
                        currentScores[label] = newScore;
                        budgetRemaining -= stepBudget;
                    }

                    string targetingBestScores = string.Join(", ", currentScores.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                    Log.TargetingCompleted(logger, targetingLabels, targetingBestScores);
                    foreach (KeyValuePair<string, double> kvp in currentScores)
                    {
                        Instruments.TargetingBestScore.Record(kvp.Value);
                    }

                    targetingActivity?.SetTag("labels", targetingLabels);
                    double bestScore = currentScores.Count > 0 ? currentScores.Values.Max() : 0.0;
                    targetingActivity?.SetTag("best_score", bestScore);
                    targetingActivity?.Stop();

                    if (failingNodes is not null)
                    {
                        Activity? shrinkActivity = ConjectureObservability.ActivitySource.StartActivity("PropertyTest.Shrinking");
                        (IReadOnlyList<IRNode> shrunk, int shrinkCount) = await Shrinker.ShrinkAsync(
                            failingNodes, n => ReplayAsync(n, test), logger);
                        shrinkActivity?.SetTag("reductions", shrinkCount);
                        shrinkActivity?.Stop();

                        if (dbContext is DbContext ctx)
                        {
                            ctx.Database.Save(ctx.TestIdHash, SerializeNodes(shrunk));
                            Instruments.DatabaseSavesTotal.Add(1);
                        }

                        Instruments.FailuresTotal.Add(1);
                        Log.PropertyTestFailure(logger, valid + 1, $"0x{seed:X16}");
                        rootActivity?.SetTag("test.status", "fail");
                        rootActivity?.Stop();
                        return TestRunResult.Fail(shrunk, failingNodes, seed, valid + 1, shrinkCount);
                    }

                    rootActivity?.SetTag("test.status", "pass");
                    rootActivity?.Stop();
                    return TestRunResult.Pass(seed, valid, currentScores);

                }
                finally
                {
                    targetingActivity?.Stop();
                }
            }

            rootActivity?.SetTag("test.status", "pass");
            rootActivity?.Stop();
            return TestRunResult.Pass(seed, valid);

        }
        finally
        {
            generationActivity?.Stop();
            rootActivity?.Stop();
        }
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

    private static async Task<(Status, IReadOnlyDictionary<string, double>)> ReplayAndObserveAsync(
        IReadOnlyList<IRNode> nodes, Func<ConjectureData, Task> test)
    {
        ConjectureData data = ConjectureData.ForRecord(nodes);
        Target.CurrentData.Value = data;
        try
        {
            await test(data);
            return (Status.Valid, data.Observations);
        }
        catch (UnsatisfiedAssumptionException)
        {
            return (Status.Invalid, data.Observations);
        }
        catch (InvalidOperationException) when (data.Status == Status.Overrun)
        {
            return (Status.Overrun, data.Observations);
        }
        catch (Exception)
        {
            return (Status.Interesting, data.Observations);
        }
        finally
        {
            Target.CurrentData.Value = null;
            data.Freeze();
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