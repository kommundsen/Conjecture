// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Core.Tests.Formatting;

public class BuiltInFormatterTests
{
    // --- int ---

    [Theory]
    [InlineData(42, "42")]
    [InlineData(0, "0")]
    [InlineData(-7, "-7")]
    [InlineData(int.MaxValue, "2147483647")]
    [InlineData(int.MinValue, "-2147483648")]
    public void Int32_Format_ProducesIntegerLiteral(int value, string expected)
    {
        var result = BuiltInFormatters.Int32.Format(value);
        Assert.Equal(expected, result);
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

    [Theory]
    [InlineData(3.14, "3.14")]
    [InlineData(0.0, "0")]
    [InlineData(-1.5, "-1.5")]
    public void Double_Format_ProducesFloatingPointLiteral(double value, string expected)
    {
        var result = BuiltInFormatters.Double.Format(value);
        Assert.Equal(expected, result);
    }

    // --- float ---

    [Theory]
    [InlineData(1.5f, "1.5f")]
    [InlineData(0.0f, "0f")]
    [InlineData(-3.0f, "-3f")]
    public void Single_Format_ProducesFloatLiteral(float value, string expected)
    {
        var result = BuiltInFormatters.Single.Format(value);
        Assert.Equal(expected, result);
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