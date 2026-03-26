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
                return TestRunResult.Fail(data.IRNodes);
            }
            finally
            {
                data.Freeze();
            }
        }

        return TestRunResult.Pass();
    }
}
