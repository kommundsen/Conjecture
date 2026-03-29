using System.Diagnostics;

using ShrinkEngine = Conjecture.Core.Internal.Shrinker.Shrinker;

namespace Conjecture.Core.Internal;

internal static class TestRunner
{
    internal static TestRunResult Run(ConjectureSettings settings, Action<ConjectureData> test)
    {
        var seed = settings.Seed ?? (ulong)Random.Shared.NextInt64();
        var rng = new SplittableRandom(seed);
        var valid = 0;
        var unsatisfied = 0;
        var totalAttempts = 0;
        var maxAttempts = settings.MaxExamples * 200;
        var deadline = settings.Deadline;
        var sw = deadline.HasValue ? Stopwatch.StartNew() : null;

        while (valid < settings.MaxExamples && totalAttempts < maxAttempts)
        {
            totalAttempts++;
            var data = ConjectureData.ForGeneration(rng.Split());
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
        var data = ConjectureData.ForRecord(nodes);
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
}
