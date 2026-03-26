using ShrinkEngine = Conjecture.Core.Internal.Shrinker.Shrinker;

namespace Conjecture.Core.Internal;

internal static class TestRunner
{
    internal static TestRunResult Run(ConjectureSettings settings, Action<ConjectureData> test)
    {
        var seed = settings.Seed ?? (ulong)Random.Shared.NextInt64();
        var rng = new SplittableRandom(seed);
        var valid = 0;
        var totalAttempts = 0;
        var maxAttempts = settings.MaxExamples * 200;

        while (valid < settings.MaxExamples && totalAttempts < maxAttempts)
        {
            totalAttempts++;
            var data = ConjectureData.ForGeneration(rng.Split());
            try
            {
                test(data);
                valid++;
            }
            catch (UnsatisfiedAssumptionException)
            {
                data.MarkInvalid();
            }
            catch
            {
                data.MarkInteresting();
                var shrunk = ShrinkEngine.Shrink(data.IRNodes, nodes => Replay(nodes, test));
                return TestRunResult.Fail(shrunk, seed);
            }
            finally
            {
                data.Freeze();
            }
        }

        return TestRunResult.Pass(seed);
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
