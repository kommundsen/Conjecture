// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Globalization;

using Conjecture.Core;

namespace Conjecture.Core.Tests.Formatting;

public class BuiltInFormatterTests
{
    // --- int ---

    [Property]
    [Sample(42)]
    [Sample(0)]
    [Sample(-7)]
    [Sample(int.MaxValue)]
    [Sample(int.MinValue)]
    public void Int32_Format_ProducesIntegerLiteral(int value)
    {
        string result = BuiltInFormatters.Int32.Format(value);
        Assert.Equal(value, int.Parse(result));
    }

    // --- bool ---

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Boolean_Format_ProducesLowercaseLiteral(bool value, string expected)
    {
        var result = BuiltInFormatters.Boolean.Format(value);
        Assert.Equal(expected, result);
    }

    // --- double ---

    [Property]
    [Sample(3.14)]
    [Sample(0.0)]
    [Sample(-1.5)]
    public void Double_Format_ProducesFloatingPointLiteral(double value)
    {
        Assume.That(double.IsFinite(value));
        string result = BuiltInFormatters.Double.Format(value);
        Assert.True(double.TryParse(result, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            $"'{result}' is not parseable as double");
    }

    // --- float ---

    [Property]
    [Sample(1.5f)]
    [Sample(0.0f)]
    [Sample(-3.0f)]
    public void Single_Format_ProducesFloatLiteral(float value)
    {
        Assume.That(float.IsFinite(value));
        string result = BuiltInFormatters.Single.Format(value);
        Assert.True(result.EndsWith("f", StringComparison.Ordinal), $"Float format should end with 'f', got '{result}'");
        Assert.True(float.TryParse(result[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            $"'{result}' is not parseable as float (after stripping 'f' suffix)");
    }

    // --- string ---

    [Fact]
    public void String_Format_WrapsInQuotes()
    {
        var result = BuiltInFormatters.String.Format("hello");
        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void String_Format_EscapesInternalQuotes()
    {
        var result = BuiltInFormatters.String.Format("say \"hi\"");
        Assert.Equal("\"say \\\"hi\\\"\"", result);
    }

    [Fact]
    public void String_Format_EscapesBackslash()
    {
        var result = BuiltInFormatters.String.Format(@"a\b");
        Assert.Equal("\"a\\\\b\"", result);
    }

    [Fact]
    public void String_Format_EscapesNewline()
    {
        var result = BuiltInFormatters.String.Format("a\nb");
        Assert.Equal("\"a\\nb\"", result);
    }

    [Fact]
    public void String_Format_EscapesTab()
    {
        var result = BuiltInFormatters.String.Format("a\tb");
        Assert.Equal("\"a\\tb\"", result);
    }

    // --- byte[] ---

    [Fact]
    public void ByteArray_Format_ProducesHexLiteral()
    {
        var result = BuiltInFormatters.ByteArray.Format([0x01, 0xFF]);
        Assert.Equal("new byte[] { 0x01, 0xFF }", result);
    }

    [Fact]
    public void ByteArray_Format_EmptyArray()
    {
        var result = BuiltInFormatters.ByteArray.Format([]);
        Assert.Equal("new byte[] {  }", result);
    }

    [Fact]
    public void ByteArray_Format_SingleByte()
    {
        var result = BuiltInFormatters.ByteArray.Format([0xAB]);
        Assert.Equal("new byte[] { 0xAB }", result);
    }
}