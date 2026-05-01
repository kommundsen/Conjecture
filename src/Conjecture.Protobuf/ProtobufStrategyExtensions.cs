// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text.Json;

using Conjecture.Core;

using Google.Protobuf;
using Google.Protobuf.Reflection;

using FieldType = Google.Protobuf.Reflection.FieldType;

namespace Conjecture.Protobuf;

/// <summary>Extension methods on <see cref="Strategy"/> for Protobuf-driven generation.</summary>
#pragma warning disable RS0026 // multiple overloads with optional parameters — established project pattern
public static class ProtobufStrategyExtensions
{
    extension(Strategy)
    {
        /// <summary>Returns a strategy that generates <see cref="JsonElement"/> objects shaped by the Protobuf message <typeparamref name="T"/>.</summary>
        public static Strategy<JsonElement> FromProtobuf<T>(int maxDepth = 5)
            where T : IMessage<T>, new()
        {
            MessageDescriptor descriptor = new T().Descriptor;
            return BuildMessageStrategy(descriptor, maxDepth);
        }

        /// <summary>Returns a strategy that generates <see cref="JsonElement"/> objects shaped by the given <paramref name="descriptor"/>.</summary>
        public static Strategy<JsonElement> FromProtobuf(MessageDescriptor descriptor, int maxDepth = 5) =>
            BuildMessageStrategy(descriptor, maxDepth);
    }

    private static Strategy<JsonElement> BuildMessageStrategy(MessageDescriptor descriptor, int depth)
    {
        Dictionary<OneofDescriptor, List<FieldDescriptor>> oneofGroups = [];
        List<FieldDescriptor> regularFields = [];

        foreach (FieldDescriptor field in descriptor.Fields.InFieldNumberOrder())
        {
            if (field.ContainingOneof is not null)
            {
                OneofDescriptor oneof = field.ContainingOneof;
                if (!oneofGroups.TryGetValue(oneof, out List<FieldDescriptor>? group))
                {
                    group = [];
                    oneofGroups[oneof] = group;
                }

                group.Add(field);
            }
            else
            {
                regularFields.Add(field);
            }
        }

        List<(string JsonName, Strategy<JsonElement> FieldStrategy, bool IsRepeated)> regularStrategies = [];
        foreach (FieldDescriptor field in regularFields)
        {
            Strategy<JsonElement> fieldStrategy = BuildFieldStrategy(field, depth);
            regularStrategies.Add((field.JsonName, fieldStrategy, field.IsRepeated));
        }

        List<Strategy<JsonElement>> oneofArmStrategies = [];
        foreach (KeyValuePair<OneofDescriptor, List<FieldDescriptor>> kvp in oneofGroups)
        {
            List<FieldDescriptor> arms = kvp.Value;
            Strategy<JsonElement>[] armStrategies = new Strategy<JsonElement>[arms.Count];
            for (int i = 0; i < arms.Count; i++)
            {
                FieldDescriptor arm = arms[i];
                Strategy<JsonElement> armFieldStrategy = BuildFieldStrategy(arm, depth);
                string jsonName = arm.JsonName;
                armStrategies[i] = armFieldStrategy.Select(v => SerializeSingleField(jsonName, v));
            }

            oneofArmStrategies.Add(Strategy.OneOf(armStrategies));
        }

        return Strategy.Compose(ctx =>
        {
            Dictionary<string, JsonElement> obj = [];

            foreach ((string jsonName, Strategy<JsonElement> strategy, bool isRepeated) in regularStrategies)
            {
                obj[jsonName] = ctx.Generate(strategy);
            }

            foreach (Strategy<JsonElement> oneofStrategy in oneofArmStrategies)
            {
                JsonElement arm = ctx.Generate(oneofStrategy);
                foreach (JsonProperty prop in arm.EnumerateObject())
                {
                    obj[prop.Name] = prop.Value.Clone();
                }
            }

            return SerializeObject(obj);
        });
    }

    private static Strategy<JsonElement> BuildFieldStrategy(FieldDescriptor field, int depth)
    {
        Strategy<JsonElement> scalarStrategy = BuildScalarStrategy(field, depth);

        return field.IsRepeated
            ? Strategy.Lists(scalarStrategy, maxSize: 3).Select(static items => SerializeArray(items))
            : scalarStrategy;
    }

    private static Strategy<JsonElement> BuildScalarStrategy(FieldDescriptor field, int depth)
    {
        return field.FieldType switch
        {
            FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32 =>
                Strategy.Integers<int>().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Int64 or FieldType.SInt64 or FieldType.SFixed64 =>
                Strategy.Integers<long>().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.UInt32 or FieldType.Fixed32 =>
                Strategy.Integers<uint>().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.UInt64 or FieldType.Fixed64 =>
                Strategy.Integers<ulong>().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Float =>
                Strategy.Floats(-1e10f, 1e10f).Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Double =>
                Strategy.Doubles(-1e10, 1e10).Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Bool =>
                Strategy.Booleans().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.String =>
                Strategy.Strings().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Bytes =>
                Strategy.Arrays(Strategy.Integers<byte>(), 16, 16).Select(static v => JsonSerializer.SerializeToElement(Convert.ToBase64String(v))),
            FieldType.Enum =>
                BuildEnumStrategy(field),
            FieldType.Message =>
                BuildMessageFieldStrategy(field, depth),
            _ =>
                Strategy.Just(JsonSerializer.SerializeToElement(string.Empty)),
        };
    }

    private static Strategy<JsonElement> BuildEnumStrategy(FieldDescriptor field)
    {
        EnumDescriptor enumDescriptor = field.EnumType;
        IList<EnumValueDescriptor> values = enumDescriptor.Values;
        int[] numbers = new int[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            numbers[i] = values[i].Number;
        }

        return Strategy.SampledFrom(numbers).Select(static n => JsonSerializer.SerializeToElement(n));
    }

    private static Strategy<JsonElement> BuildMessageFieldStrategy(FieldDescriptor field, int depth)
    {
        MessageDescriptor nestedDescriptor = field.MessageType;

        string fullName = nestedDescriptor.FullName;
        if (fullName is "google.protobuf.Timestamp" or "google.protobuf.Duration")
        {
            return Strategy.Strings().Select(static v => JsonSerializer.SerializeToElement(v));
        }

        if (fullName is "google.protobuf.Any")
        {
            return Strategy.Just(JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["@type"] = "type.googleapis.com/google.protobuf.Empty",
            }));
        }

        if (depth <= 0)
        {
            return Strategy.Just(SerializeObject([]));
        }

        return BuildMessageStrategy(nestedDescriptor, depth - 1);
    }

    private static JsonElement SerializeSingleField(string jsonName, JsonElement value)
    {
        Dictionary<string, JsonElement> single = new() { [jsonName] = value };
        return SerializeObject(single);
    }

    private static JsonElement SerializeArray(List<JsonElement> items) =>
        JsonSerializer.SerializeToElement(items);

    private static JsonElement SerializeObject(Dictionary<string, JsonElement> obj) =>
        JsonSerializer.SerializeToElement(obj);
}
#pragma warning restore RS0026
