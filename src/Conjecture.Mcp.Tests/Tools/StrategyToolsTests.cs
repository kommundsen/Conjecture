// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class StrategyToolsTests
{
    [Theory]
    [InlineData("bool", "Generate.Booleans()")]
    [InlineData("int", "Generate.Integers<int>()")]
    [InlineData("long", "Generate.Integers<long>()")]
    [InlineData("float", "Generate.Floats()")]
    [InlineData("double", "Generate.Doubles()")]
    [InlineData("string", "Generate.Strings()")]
    [InlineData("byte[]", "Generate.Bytes(size)")]
    public void SuggestForType_KnownPrimitive_ContainsExpectedCall(string typeName, string expectedCall)
    {
        var result = StrategyTools.SuggestForType(typeName);
        Assert.Contains(expectedCall, result);
    }

    [Theory]
    [InlineData("List<int>", "Generate.Lists(")]
    [InlineData("IReadOnlyList<string>", "Generate.Lists(")]
    [InlineData("IEnumerable<bool>", "Generate.Lists(")]
    [InlineData("HashSet<int>", "Generate.Sets(")]
    [InlineData("IReadOnlySet<string>", "Generate.Sets(")]
    public void SuggestForType_Collection_ContainsExpectedFactory(string typeName, string expectedFactory)
    {
        var result = StrategyTools.SuggestForType(typeName);
        Assert.Contains(expectedFactory, result);
    }

    [Theory]
    [InlineData("Dictionary<string, int>", "Generate.Dictionaries(")]
    [InlineData("IReadOnlyDictionary<int, bool>", "Generate.Dictionaries(")]
    public void SuggestForType_Dictionary_ContainsDictionariesFactory(string typeName, string expectedFactory)
    {
        var result = StrategyTools.SuggestForType(typeName);
        Assert.Contains(expectedFactory, result);
    }

    [Theory]
    [InlineData("int?")]
    [InlineData("Nullable<int>")]
    public void SuggestForType_Nullable_ContainsNullableOrOrNull(string typeName)
    {
        var result = StrategyTools.SuggestForType(typeName);
        Assert.True(result.Contains("Generate.Nullable(") || result.Contains(".OrNull()"),
            $"Expected Nullable or OrNull in: {result}");
    }

    [Fact]
    public void SuggestForType_Tuple_MentionsTuples()
    {
        var result = StrategyTools.SuggestForType("(int, string)");
        Assert.Contains("Generate.Tuples(", result);
    }

    [Fact]
    public void SuggestForType_CustomType_MentionsCompose()
    {
        var result = StrategyTools.SuggestForType("MyRecord");
        Assert.Contains("Generate.Compose", result);
    }

    [Fact]
    public void SuggestForSealedAbstractType_SealedAbstractBase_ContainsArbitraryAttribute()
    {
        string result = StrategyTools.SuggestForSealedAbstractType("MyAbstractBase");
        Assert.Contains("[Arbitrary]", result);
    }

    [Fact]
    public void SuggestForSealedAbstractType_SealedAbstractBase_ContainsGenerateOneOf()
    {
        string result = StrategyTools.SuggestForSealedAbstractType("MyAbstractBase");
        Assert.Contains("Generate.OneOf", result);
    }

    [Fact]
    public void SuggestForSealedAbstractType_SealedAbstractBase_DoesNotMentionComposeAsMainRecommendation()
    {
        string result = StrategyTools.SuggestForSealedAbstractType("MyAbstractBase");
        Assert.DoesNotContain("Generate.Compose (recommended", result);
    }

    [Fact]
    public void SuggestForSealedAbstractType_SealedAbstractBase_MentionsArbitraryOnAbstractAndConcreteTypes()
    {
        string result = StrategyTools.SuggestForSealedAbstractType("Shape");
        Assert.True(
            result.Contains("[Arbitrary]"),
            "Should mention [Arbitrary] attribute for strategy implementation");
    }

    [Fact]
    public void SuggestForType_Regex_ContainsGenMatching()
    {
        string result = StrategyTools.SuggestForType("Regex");
        Assert.True(
            result.Contains("Gen.Matching") || result.Contains("RegexGenerate.Matching"),
            $"Expected Gen.Matching or RegexGenerate.Matching in: {result}");
    }

    [Theory]
    [InlineData("email")]
    [InlineData("Email")]
    public void SuggestForType_EmailKeyword_ContainsRegexGenerateEmail(string typeName)
    {
        string result = StrategyTools.SuggestForType(typeName);
        Assert.Contains("RegexGenerate.Email()", result);
    }
}