using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit.V3;
using Xunit;

namespace Conjecture.SelfTests;

[Arbitrary]
public partial record SelfPoint(int X, int Y);

[Arbitrary]
public partial record SelfLabel(string Text);

[Arbitrary]
public partial record SelfEvent(SelfLabel Label, bool Active);

public class GeneratorSelfTests
{
    [Property(MaxExamples = 20, Seed = 1UL)]
    public void GeneratedRecord_UsedAsPropertyParameter_GeneratesValidInstances(SelfPoint p)
    {
        Assert.NotNull(p);
    }

    [Fact]
    public async Task GeneratedStrategy_Select_TransformsValues()
    {
        Strategy<int> xStrategy = new SelfPointArbitrary().Create().Select(p => p.X);
        ConjectureSettings settings = new() { Seed = 2UL, MaxExamples = 20, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = xStrategy.Generate(data);
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task GeneratedStrategy_Where_FiltersValues()
    {
        Strategy<SelfPoint> positivePoints = new SelfPointArbitrary().Create()
            .Where(p => p.X > 0 && p.Y > 0);
        ConjectureSettings settings = new() { Seed = 3UL, MaxExamples = 20, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            SelfPoint p = positivePoints.Generate(data);
            if (p.X <= 0 || p.Y <= 0) throw new InvalidOperationException("filter violated");
        });

        Assert.True(result.Passed);
    }

    [Property(MaxExamples = 20, Seed = 4UL)]
    public void GeneratedNestedRecord_CrossReference_ResolvesInnerType(SelfEvent e)
    {
        Assert.NotNull(e);
        Assert.NotNull(e.Label);
        Assert.NotNull(e.Label.Text);
    }

    [Fact]
    public async Task GeneratedStrategy_SelectMany_ComposesCorrectly()
    {
        Strategy<SelfLabel> labelStrategy = new SelfLabelArbitrary().Create();
        Strategy<(SelfPoint Point, SelfLabel Label)> paired =
            new SelfPointArbitrary().Create()
                .SelectMany(p => labelStrategy.Select(l => (Point: p, Label: l)));

        ConjectureSettings settings = new() { Seed = 5UL, MaxExamples = 20, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            (SelfPoint point, SelfLabel label) = paired.Generate(data);
            Assert.NotNull(point);
            Assert.NotNull(label);
        });

        Assert.True(result.Passed);
    }
}
