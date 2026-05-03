// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class StrategyToolsTests
{
    [Theory]
    [InlineData("bool", "Strategy.Booleans()")]
    [InlineData("int", "Strategy.Integers<int>()")]
    [InlineData("long", "Strategy.Integers<long>()")]
    [InlineData("float", "Strategy.Floats()")]
    [InlineData("double", "Strategy.Doubles()")]
    [InlineData("string", "Strategy.Strings()")]
    [InlineData("byte[]", "Strategy.Arrays(Strategy.Integers<byte>()")]
    [InlineData("Guid", "Strategy.Guids")]
    public void SuggestForType_KnownPrimitive_ContainsExpectedCall(string typeName, string expectedCall)
    {
        var result = StrategyTools.SuggestForType(typeName);
        Assert.Contains(expectedCall, result);
    }

    [Theory]
    [InlineData("List<int>", "Strategy.Lists(")]
    [InlineData("IReadOnlyList<string>", "Strategy.Lists(")]
    [InlineData("IEnumerable<bool>", "Strategy.Lists(")]
    [InlineData("HashSet<int>", "Strategy.Sets(")]
    [InlineData("IReadOnlySet<string>", "Strategy.Sets(")]
    public void SuggestForType_Collection_ContainsExpectedFactory(string typeName, string expectedFactory)
    {
        var result = StrategyTools.SuggestForType(typeName);
        Assert.Contains(expectedFactory, result);
    }

    [Theory]
    [InlineData("Dictionary<string, int>", "Strategy.Dictionaries(")]
    [InlineData("IReadOnlyDictionary<int, bool>", "Strategy.Dictionaries(")]
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
        Assert.True(result.Contains("Strategy.Nullable(") || result.Contains(".OrNull()"),
            $"Expected Nullable or OrNull in: {result}");
    }

    [Fact]
    public void SuggestForType_Tuple_MentionsTuples()
    {
        var result = StrategyTools.SuggestForType("(int, string)");
        Assert.Contains("Strategy.Tuples(", result);
    }

    [Fact]
    public void SuggestForType_CustomType_MentionsCompose()
    {
        var result = StrategyTools.SuggestForType("MyRecord");
        Assert.Contains("Strategy.Compose", result);
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
        Assert.Contains("Strategy.OneOf", result);
    }

    [Fact]
    public void SuggestForSealedAbstractType_SealedAbstractBase_DoesNotMentionComposeAsMainRecommendation()
    {
        string result = StrategyTools.SuggestForSealedAbstractType("MyAbstractBase");
        Assert.DoesNotContain("Strategy.Compose (recommended", result);
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
            result.Contains("Strategy.Matching"),
            $"Expected Strategy.Matching in: {result}");
    }

    [Theory]
    [InlineData("email")]
    [InlineData("Email")]
    public void SuggestForType_EmailKeyword_ContainsGenerateEmail(string typeName)
    {
        string result = StrategyTools.SuggestForType(typeName);
        Assert.Contains("Strategy.Email()", result);
    }

    [Fact]
    public void SuggestForType_WithArbitraryAttribute_ReturnsGenerateFor()
    {
        string result = StrategyTools.SuggestForType("MyRecord", hasArbitraryAttribute: true);
        Assert.Contains("Strategy.For<MyRecord>()", result);
    }

    [Fact]
    public void SuggestForType_WithArbitraryAttribute_IncludesOverrideDsl()
    {
        string result = StrategyTools.SuggestForType("MyDto", hasArbitraryAttribute: true);
        Assert.Contains("cfg => cfg.Override", result);
    }

    [Fact]
    public void SuggestForType_WithArbitraryAttribute_DoesNotRecommendCompose()
    {
        string result = StrategyTools.SuggestForType("MyRecord", hasArbitraryAttribute: true);
        Assert.DoesNotContain("Strategy.Compose (recommended", result);
    }

    [Fact]
    public void SuggestForType_WithoutArbitraryAttribute_FallsBackToCompose()
    {
        string result = StrategyTools.SuggestForType("MyRecord");
        Assert.Contains("Strategy.Compose", result);
    }

    [Fact]
    public void SuggestStrategy_WithArbitraryAttribute_ReturnsGenerateFor()
    {
        string result = StrategyTools.SuggestStrategy(typeName: "MyRecord", hasArbitraryAttribute: true);
        Assert.Contains("Strategy.For<MyRecord>()", result);
    }

    [Fact]
    public void SuggestForType_ReDoS_ContainsReDoSHunter()
    {
        string result = StrategyTools.SuggestForType("ReDoS");
        Assert.Contains("ReDoSHunter", result);
    }

    [Theory]
    [InlineData("backtracking")]
    [InlineData("catastrophic")]
    [InlineData("adversarial")]
    public void SuggestForType_ReDoSKeywords_ContainsReDoSHunter(string typeName)
    {
        string result = StrategyTools.SuggestForType(typeName);
        Assert.Contains("ReDoSHunter", result);
    }

    [Fact]
    public void SuggestForType_Decimal_ContainsGenerateDecimal()
    {
        string result = StrategyTools.SuggestForType("decimal");
        Assert.Contains("Strategy.Decimal(", result);
    }

    [Fact]
    public void SuggestForType_Decimal_MentionsConjectureMoney()
    {
        string result = StrategyTools.SuggestForType("decimal");
        Assert.Contains("Conjecture.Money", result);
    }

    [Theory]
    [InlineData("currency")]
    [InlineData("currencies")]
    [InlineData("currencyCode")]
    [InlineData("ISO4217")]
    [InlineData("ISO 4217")]
    public void SuggestForType_CurrencyKeyword_ContainsIso4217Codes(string typeName)
    {
        string result = StrategyTools.SuggestForType(typeName);
        Assert.Contains("Strategy.Iso4217Codes()", result);
    }

    [Fact]
    public void SuggestForType_CurrencyKeyword_ContainsAmounts()
    {
        string result = StrategyTools.SuggestForType("currency");
        Assert.Contains("Strategy.Amounts(", result);
    }

    [Fact]
    public void SuggestStrategy_Currency_MentionsCulturesWithCurrency()
    {
        string result = StrategyTools.SuggestForType("currency");
        Assert.Contains("CulturesWithCurrency", result);
    }

    [Fact]
    public void SuggestStrategy_Currency_MentionsCulturesByCurrencyCode()
    {
        string result = StrategyTools.SuggestForType("currency");
        Assert.Contains("CulturesByCurrencyCode", result);
    }

    [Fact]
    public void SuggestForType_MidpointRounding_ContainsRoundingModes()
    {
        string result = StrategyTools.SuggestForType("MidpointRounding");
        Assert.Contains("Strategy.RoundingModes()", result);
    }

    [Theory]
    [InlineData("money")]
    [InlineData("amount")]
    [InlineData("price")]
    public void SuggestForType_MoneyKeyword_ContainsAmounts(string typeName)
    {
        string result = StrategyTools.SuggestForType(typeName);
        Assert.Contains("Strategy.Amounts(", result);
    }

    [Fact]
    public void SuggestForType_RoundingKeyword_ContainsRoundingModes()
    {
        string result = StrategyTools.SuggestForType("rounding");
        Assert.Contains("Strategy.RoundingModes()", result);
    }

    [Fact]
    public void SuggestForType_Version_ContainsVersionsFactory()
    {
        string result = StrategyTools.SuggestForType("Version");
        Assert.Contains("Strategy.Versions()", result);
    }

    [Fact]
    public void SuggestForType_Version_MentionsStrategyOfVersion()
    {
        string result = StrategyTools.SuggestForType("Version");
        Assert.Contains("Strategy<Version>", result);
    }

    [Fact]
    public void SuggestForKnownType_IPAddress_RecommendsIPAddressesFactory()
    {
        string result = StrategyTools.SuggestForType("IPAddress");
        Assert.Contains("Strategy.IPAddresses()", result);
    }

    [Fact]
    public void SuggestForKnownType_IPEndPoint_RecommendsIPEndPointsFactory()
    {
        string result = StrategyTools.SuggestForType("IPEndPoint");
        Assert.Contains("Strategy.IPEndPoints()", result);
    }

    [Fact]
    public void SuggestForKnownType_Uri_RecommendsUrisFactory()
    {
        string result = StrategyTools.SuggestForType("Uri");
        Assert.Contains("Strategy.Uris()", result);
    }

    [Fact]
    public void SuggestForKnownType_MailAddress_RecommendsEmailAddressesFactory()
    {
        string result = StrategyTools.SuggestForType("MailAddress");
        Assert.Contains("Strategy.EmailAddresses()", result);
    }
}