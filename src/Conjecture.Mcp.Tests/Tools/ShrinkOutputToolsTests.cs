// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class ShrinkOutputToolsTests
{
    private const string TypicalOutput =
        """
        Falsifying example found after 47 examples (shrunk 23 times)
        x = -847362
        list = [5, 2, 99]
        Minimal counterexample:
        x = 1
        list = [0]
        Reproduce with: [Property(Seed = 0xABCD1234)]
        """;

    [Fact]
    public void Parse_TypicalOutput_MentionsExampleCount()
    {
        var result = ShrinkOutputTools.Parse(TypicalOutput);
        Assert.Contains("47", result);
    }

    [Fact]
    public void Parse_TypicalOutput_MentionsShrinkCount()
    {
        var result = ShrinkOutputTools.Parse(TypicalOutput);
        Assert.Contains("23", result);
    }

    [Fact]
    public void Parse_TypicalOutput_ShowsMinimalCounterexample()
    {
        var result = ShrinkOutputTools.Parse(TypicalOutput);
        Assert.Contains("`x` = `1`", result);
    }

    [Fact]
    public void Parse_TypicalOutput_IncludesReproductionGuidance()
    {
        var result = ShrinkOutputTools.Parse(TypicalOutput);
        Assert.Contains("0xABCD1234", result);
    }

    [Fact]
    public void Parse_ExplicitExample_IdentifiesAsExplicit()
    {
        const string explicitOutput =
            """
            Explicit example failed:
              a = 0
              b = 0
            Division by zero
            """;

        var result = ShrinkOutputTools.Parse(explicitOutput);
        Assert.Contains("Explicit", result);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsGracefulMessage()
    {
        var result = ShrinkOutputTools.Parse(string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void Parse_UnrecognizedInput_ReturnsFormatHelp()
    {
        var result = ShrinkOutputTools.Parse("Some random text with no Conjecture formatting");
        Assert.Contains("parse", result, StringComparison.OrdinalIgnoreCase);
    }
}
