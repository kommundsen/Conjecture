// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.IO;

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class ValueRendererTests
{
    // helper record used for JSON-deserialization fallback tests
    private record Person(string Name, int Age);

    // --- null ---

    [Fact]
    public void RenderLiteral_NullValue_EmitsCastNullSyntax()
    {
        string result = ValueRenderer.RenderLiteral("x", null, typeof(int));

        Assert.Equal("var x = (int)null!;", result);
    }

    [Fact]
    public void RenderLiteral_NullValueReferenceType_EmitsCastNullSyntax()
    {
        string result = ValueRenderer.RenderLiteral("s", null, typeof(string));

        Assert.Equal("var s = (string)null!;", result);
    }

    [Fact]
    public void RenderLiteral_NullableIntNull_EmitsQuestionMarkTypeSyntax()
    {
        string result = ValueRenderer.RenderLiteral("n", null, typeof(int?));

        Assert.Equal("var n = (int?)null!;", result);
    }

    // --- built-in formatters via FormatterRegistry ---

    [Fact]
    public void RenderLiteral_Int_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral("n", 42, typeof(int));

        Assert.Equal("var n = 42;", result);
    }

    [Fact]
    public void RenderLiteral_Bool_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral("b", true, typeof(bool));

        Assert.Equal("var b = true;", result);
    }

    [Fact]
    public void RenderLiteral_Double_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral("d", 3.14, typeof(double));

        Assert.Equal("var d = 3.14;", result);
    }

    [Fact]
    public void RenderLiteral_Float_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral("f", 1.5f, typeof(float));

        Assert.Equal("var f = 1.5f;", result);
    }

    [Fact]
    public void RenderLiteral_String_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral("s", "hello", typeof(string));

        Assert.Equal("var s = \"hello\";", result);
    }

    [Fact]
    public void RenderLiteral_ByteArray_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral("buf", new byte[] { 0x01, 0xFF }, typeof(byte[]));

        Assert.Equal("var buf = new byte[] { 0x01, 0xFF };", result);
    }

    [Fact]
    public void RenderLiteral_ListOfInt_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral("lst", new List<int> { 1, 2, 3 }, typeof(List<int>));

        Assert.Equal("var lst = [1, 2, 3];", result);
    }

    [Fact]
    public void RenderLiteral_HashSetOfString_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral("hs", new HashSet<string> { "a" }, typeof(HashSet<string>));

        Assert.Equal("var hs = {\"a\"};", result);
    }

    [Fact]
    public void RenderLiteral_DictionaryStringInt_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral(
            "dict",
            new Dictionary<string, int> { ["k"] = 1 },
            typeof(Dictionary<string, int>));

        Assert.Equal("var dict = {\"k\": 1};", result);
    }

    [Fact]
    public void RenderLiteral_TupleIntString_UsesFormatterLiteral()
    {
        string result = ValueRenderer.RenderLiteral("t", (7, "x"), typeof((int, string)));

        Assert.Equal("var t = (7, \"x\");", result);
    }

    [Fact]
    public void RenderLiteral_IntArray_EmitsJsonDeserializeWithCorrectTypeName()
    {
        string result = ValueRenderer.RenderLiteral("arr", new int[] { 1, 2, 3 }, typeof(int[]));

        Assert.Equal("var arr = JsonSerializer.Deserialize<int[]>(\"\"\"[1,2,3]\"\"\");", result);
    }

    // --- JSON deserialization fallback ---

    [Fact]
    public void RenderLiteral_JsonSerializableRecord_EmitsDeserializeExpression()
    {
        Person person = new("Alice", 30);

        string result = ValueRenderer.RenderLiteral("p", person, typeof(Person));

        Assert.StartsWith("var p = JsonSerializer.Deserialize<", result);
        Assert.Contains("Person", result);
        Assert.Contains("Alice", result);
        Assert.Contains("30", result);
        Assert.EndsWith(";", result);
    }

    [Fact]
    public void RenderLiteral_JsonSerializableRecord_UsesRawStringLiteralDelimiters()
    {
        Person person = new("Bob", 25);

        string result = ValueRenderer.RenderLiteral("p", person, typeof(Person));

        Assert.Contains("\"\"\"", result);
    }

    // --- non-serializable fallback (WARNING comment block) ---

    [Fact]
    public void RenderLiteral_NonSerializableType_EmitsWarningComment()
    {
        MemoryStream stream = new();

        string result = ValueRenderer.RenderLiteral("ms", stream, typeof(MemoryStream));

        Assert.Contains("// WARNING:", result);
        Assert.Contains("MemoryStream", result);
    }

    [Fact]
    public void RenderLiteral_NonSerializableType_EmitsValueToStringLine()
    {
        MemoryStream stream = new();

        string result = ValueRenderer.RenderLiteral("ms", stream, typeof(MemoryStream));

        Assert.Contains("// Value was:", result);
    }

    [Fact]
    public void RenderLiteral_NonSerializableType_EmitsDefaultExpression()
    {
        MemoryStream stream = new();

        string result = ValueRenderer.RenderLiteral("ms", stream, typeof(MemoryStream));

        Assert.Contains("var ms = default(MemoryStream)!;", result);
    }

    [Fact]
    public void RenderLiteral_NonSerializableType_OutputIsThreeLines()
    {
        MemoryStream stream = new();

        string result = ValueRenderer.RenderLiteral("ms", stream, typeof(MemoryStream));

        string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
    }
}