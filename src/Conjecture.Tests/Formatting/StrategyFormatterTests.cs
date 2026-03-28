using Conjecture.Core.Formatting;

namespace Conjecture.Tests.Formatting;

public class StrategyFormatterTests
{
    private sealed class IntLiteralFormatter : IStrategyFormatter<int>
    {
        public string Format(int value) => value.ToString();
    }

    private sealed class TaggedFormatter : IStrategyFormatter<int>
    {
        public string Format(int value) => $"<{value}>";
    }

    [Fact]
    public void Format_PositiveInt_ReturnsExpectedString()
    {
        IStrategyFormatter<int> formatter = new IntLiteralFormatter();

        var result = formatter.Format(42);

        Assert.Equal("42", result);
    }

    [Fact]
    public void Format_NegativeInt_ReturnsExpectedString()
    {
        IStrategyFormatter<int> formatter = new IntLiteralFormatter();

        var result = formatter.Format(-7);

        Assert.Equal("-7", result);
    }

    [Fact]
    public void Format_Zero_ReturnsExpectedString()
    {
        IStrategyFormatter<int> formatter = new IntLiteralFormatter();

        var result = formatter.Format(0);

        Assert.Equal("0", result);
    }

    [Theory]
    [InlineData(1, "<1>")]
    [InlineData(42, "<42>")]
    [InlineData(-3, "<-3>")]
    public void Format_WithTaggedFormatter_WrapsValueInAngledBrackets(int value, string expected)
    {
        IStrategyFormatter<int> formatter = new TaggedFormatter();

        var result = formatter.Format(value);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_ReceivesExactValuePassed()
    {
        var formatter = new CapturingFormatter();

        formatter.Format(99);

        Assert.Equal(99, formatter.LastValue);
    }

    private sealed class CapturingFormatter : IStrategyFormatter<int>
    {
        public int LastValue { get; private set; }
        public string Format(int value) { LastValue = value; return value.ToString(); }
    }
}
