using Conjecture.Core.Formatting;

namespace Conjecture.Tests.Formatting;

public class CollectionFormatterTests
{
    // --- List<int> ---

    [Fact]
    public void ListInt_Format_ProducesBracketedList()
    {
        var formatter = FormatterRegistry.Get<List<int>>();
        Assert.NotNull(formatter);
        var result = formatter.Format([3, -1, 7]);
        Assert.Equal("[3, -1, 7]", result);
    }

    [Fact]
    public void ListInt_Format_EmptyList()
    {
        var formatter = FormatterRegistry.Get<List<int>>();
        Assert.NotNull(formatter);
        var result = formatter.Format([]);
        Assert.Equal("[]", result);
    }

    [Fact]
    public void ListInt_Format_SingleElement()
    {
        var formatter = FormatterRegistry.Get<List<int>>();
        Assert.NotNull(formatter);
        var result = formatter.Format([42]);
        Assert.Equal("[42]", result);
    }

    // --- HashSet<string> ---

    [Fact]
    public void HashSetString_Format_EmptySet()
    {
        var formatter = FormatterRegistry.Get<HashSet<string>>();
        Assert.NotNull(formatter);
        var result = formatter.Format([]);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void HashSetString_Format_SingleElement()
    {
        var formatter = FormatterRegistry.Get<HashSet<string>>();
        Assert.NotNull(formatter);
        var result = formatter.Format(["hello"]);
        Assert.Equal("{\"hello\"}", result);
    }

    [Fact]
    public void HashSetString_Format_MultiElement_ContainsAllElements()
    {
        var formatter = FormatterRegistry.Get<HashSet<string>>();
        Assert.NotNull(formatter);
        var result = formatter.Format(["a", "b", "c"]);
        Assert.StartsWith("{", result);
        Assert.EndsWith("}", result);
        Assert.Contains("\"a\"", result);
        Assert.Contains("\"b\"", result);
        Assert.Contains("\"c\"", result);
    }

    // --- Dictionary<int, string> ---

    [Fact]
    public void DictionaryIntString_Format_ProducesCurlyBraceMap()
    {
        var formatter = FormatterRegistry.Get<Dictionary<int, string>>();
        Assert.NotNull(formatter);
        var result = formatter.Format(new Dictionary<int, string> { [1] = "a", [2] = "b" });
        Assert.Equal("{1: \"a\", 2: \"b\"}", result);
    }

    [Fact]
    public void DictionaryIntString_Format_EmptyDictionary()
    {
        var formatter = FormatterRegistry.Get<Dictionary<int, string>>();
        Assert.NotNull(formatter);
        var result = formatter.Format([]);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void DictionaryIntString_Format_SingleEntry()
    {
        var formatter = FormatterRegistry.Get<Dictionary<int, string>>();
        Assert.NotNull(formatter);
        var result = formatter.Format(new Dictionary<int, string> { [42] = "x" });
        Assert.Equal("{42: \"x\"}", result);
    }

    // --- (int, string) tuple ---

    [Fact]
    public void TupleIntString_Format_ProducesParenthesizedTuple()
    {
        var formatter = FormatterRegistry.Get<(int, string)>();
        Assert.NotNull(formatter);
        var result = formatter.Format((3, "x"));
        Assert.Equal("(3, \"x\")", result);
    }

    [Fact]
    public void TupleIntString_Format_NegativeAndEscaped()
    {
        var formatter = FormatterRegistry.Get<(int, string)>();
        Assert.NotNull(formatter);
        var result = formatter.Format((-1, "say \"hi\""));
        Assert.Equal("(-1, \"say \\\"hi\\\"\")", result);
    }

    // --- Element formatter fallback ---

    [Fact]
    public void ListObject_Format_FallsBackToToString()
    {
        // object has no registered formatter; should fall back to ToString()
        var formatter = FormatterRegistry.Get<List<object>>();
        Assert.NotNull(formatter);
        var obj = new object();
        var result = formatter.Format([obj]);
        Assert.Equal($"[{obj}]", result);
    }
}
