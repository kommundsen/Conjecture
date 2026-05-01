// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies.Recursive;

public class JsonValueTests
{
    private abstract class JsonValue { }
    private sealed class JNull : JsonValue { }

    private sealed class JBool(bool value) : JsonValue
    {
        internal bool Value { get; } = value;
    }

    private sealed class JNumber(double value) : JsonValue
    {
        internal double Value { get; } = value;
    }

    private sealed class JString(string value) : JsonValue
    {
        internal string Value { get; } = value;
    }

    private sealed class JArray(IReadOnlyList<JsonValue> items) : JsonValue
    {
        internal IReadOnlyList<JsonValue> Items { get; } = items;
    }

    private sealed class JObject(IReadOnlyDictionary<string, JsonValue> properties) : JsonValue
    {
        internal IReadOnlyDictionary<string, JsonValue> Properties { get; } = properties;
    }

    private static string ToJson(JsonValue value)
    {
        return value switch
        {
            JNull => "null",
            JBool b => b.Value ? "true" : "false",
            JNumber n => JsonSerializer.Serialize(n.Value),
            JString s => JsonSerializer.Serialize(s.Value),
            JArray a => "[" + string.Join(",", a.Items.Select(ToJson)) + "]",
            JObject o => "{" + string.Join(",", o.Properties.Select(
                kv => JsonSerializer.Serialize(kv.Key) + ":" + ToJson(kv.Value))) + "}",
            _ => throw new InvalidOperationException($"Unknown JsonValue: {value.GetType().Name}"),
        };
    }

    private static int JsonDepth(JsonValue value)
    {
        return value switch
        {
            JNull or JBool or JNumber or JString => 0,
            JArray a => a.Items.Count == 0 ? 0 : 1 + a.Items.Max(JsonDepth),
            JObject o => o.Properties.Count == 0 ? 0 : 1 + o.Properties.Values.Max(JsonDepth),
            _ => throw new InvalidOperationException($"Unknown JsonValue: {value.GetType().Name}"),
        };
    }

    private static readonly Strategy<JsonValue> ScalarStrategy = Strategy.OneOf(
        Strategy.Just<JsonValue>(new JNull()),
        Strategy.Booleans().Select(b => (JsonValue)new JBool(b)),
        Strategy.Doubles(-1000, 1000).Select(d => (JsonValue)new JNumber(d)),
        Strategy.Strings(0, 10).Select(s => (JsonValue)new JString(s)));

    private static Strategy<JsonValue> JsonStrategy(int maxDepth)
    {
        return Strategy.Recursive<JsonValue>(
            ScalarStrategy,
            self => Strategy.OneOf(
                ScalarStrategy,
                Strategy.Lists(self, 0, 5).Select(items => (JsonValue)new JArray(items)),
                Strategy.Dictionaries(Strategy.Strings(1, 8), self, 0, 3)
                    .Select(d => (JsonValue)new JObject(d))),
            maxDepth);
    }

    [Fact]
    public async Task JsonValue_AllGeneratedValues_SerializeToValidJson()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };
        Strategy<JsonValue> strategy = JsonStrategy(maxDepth: 3);

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            JsonValue value = strategy.Generate(data);
            string json = ToJson(value);
            JsonDocument.Parse(json).Dispose();
        });

        Assert.True(result.Passed);
    }

    [Property]
    [Sample(0)]
    [Sample(3)]
    [Sample(7)]
    public void JsonValue_GenerationRespectsMaxDepth(int maxDepth)
    {
        Assume.That(maxDepth >= 0 && maxDepth <= 10);
        Strategy<JsonValue> strategy = JsonStrategy(maxDepth);
        Assert.All(strategy.WithSeed(1UL).Sample(200), value =>
            Assert.True(JsonDepth(value) <= maxDepth,
                $"JSON depth {JsonDepth(value)} exceeds maxDepth {maxDepth}"));
    }

    [Fact]
    public async Task JsonValue_ContainerProperty_FindsCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };
        Strategy<JsonValue> strategy = JsonStrategy(maxDepth: 3);

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            JsonValue value = strategy.Generate(data);
            if (value is JArray || value is JObject)
            {
                throw new InvalidOperationException("container value found");
            }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
    }

    [Fact]
    public async Task JsonValue_ContainerCounterexample_ShrinksToEmptyContainer()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };
        Strategy<JsonValue> strategy = JsonStrategy(maxDepth: 3);
        JsonValue? lastFailingValue = null;

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            JsonValue value = strategy.Generate(data);
            if (value is JArray || value is JObject)
            {
                lastFailingValue = value;
                throw new InvalidOperationException("container value found");
            }
        });

        Assert.False(result.Passed);
        Assert.NotNull(lastFailingValue);

        int itemCount = lastFailingValue switch
        {
            JArray a => a.Items.Count,
            JObject o => o.Properties.Count,
            _ => throw new InvalidOperationException("unexpected non-container"),
        };

        Assert.Equal(0, itemCount);
    }
}