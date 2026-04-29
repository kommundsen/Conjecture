// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text.Json;

using Conjecture.Core;
using Conjecture.Core.Internal;

using Google.Protobuf.Reflection;

using FieldType = Google.Protobuf.Reflection.FieldType;

// Local alias: this class extends Strategy<T> and inherits a Generate(ConjectureData) method,
// so unqualified static-class access needs a non-conflicting alias.
using Gen = Conjecture.Core.Strategy;

namespace Conjecture.Protobuf;

/// <summary>Strategy that generates <see cref="JsonElement"/> objects shaped by a Protobuf <see cref="MessageDescriptor"/>.</summary>
/// <remarks>Initializes a new instance of <see cref="ProtobufFieldStrategy"/>.</remarks>
public sealed class ProtobufFieldStrategy(MessageDescriptor descriptor, int maxDepth = 5) : Strategy<JsonElement>
{
    private readonly Strategy<JsonElement> inner = BuildMessageStrategy(descriptor, maxDepth);

    internal override JsonElement Generate(ConjectureData data)
    {
        return inner.Generate(data);
    }

    private static Strategy<JsonElement> BuildMessageStrategy(MessageDescriptor descriptor, int depth)
    {
        // Group fields by oneof index; fields not in a oneof have ContainingOneof == null.
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

        // Pre-build strategies for all regular fields.
        List<(string JsonName, Strategy<JsonElement> FieldStrategy, bool IsRepeated)> regularStrategies = [];
        foreach (FieldDescriptor field in regularFields)
        {
            Strategy<JsonElement> fieldStrategy = BuildFieldStrategy(field, depth);
            regularStrategies.Add((field.JsonName, fieldStrategy, field.IsRepeated));
        }

        // Pre-build strategies for each oneof group (each arm generates a single-field object).
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

            oneofArmStrategies.Add(Gen.OneOf(armStrategies));
        }

        return Gen.Compose(ctx =>
        {
            Dictionary<string, JsonElement> obj = [];

            foreach ((string jsonName, Strategy<JsonElement> strategy, bool isRepeated) in regularStrategies)
            {
                obj[jsonName] = ctx.Generate(strategy);
            }

            // For each oneof group, pick exactly one arm and merge its single field.
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

    private static JsonElement SerializeSingleField(string jsonName, JsonElement value)
    {
        Dictionary<string, JsonElement> single = new() { [jsonName] = value };
        return SerializeObject(single);
    }

    private static Strategy<JsonElement> BuildFieldStrategy(FieldDescriptor field, int depth)
    {
        Strategy<JsonElement> scalarStrategy = BuildScalarStrategy(field, depth);

        return field.IsRepeated
            ? Gen.Lists(scalarStrategy, maxSize: 3).Select(static items => SerializeArray(items))
            : scalarStrategy;
    }

    private static Strategy<JsonElement> BuildScalarStrategy(FieldDescriptor field, int depth)
    {
        return field.FieldType switch
        {
            FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32 =>
                Gen.Integers<int>().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Int64 or FieldType.SInt64 or FieldType.SFixed64 =>
                Gen.Integers<long>().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.UInt32 or FieldType.Fixed32 =>
                Gen.Integers<uint>().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.UInt64 or FieldType.Fixed64 =>
                Gen.Integers<ulong>().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Float =>
                Gen.Floats(-1e10f, 1e10f).Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Double =>
                Gen.Doubles(-1e10, 1e10).Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Bool =>
                Gen.Booleans().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.String =>
                Gen.Strings().Select(static v => JsonSerializer.SerializeToElement(v)),
            FieldType.Bytes =>
                Gen.Bytes(16).Select(static v => JsonSerializer.SerializeToElement(Convert.ToBase64String(v))),
            FieldType.Enum =>
                BuildEnumStrategy(field),
            FieldType.Message =>
                BuildMessageFieldStrategy(field, depth),
            _ =>
                Gen.Just(JsonSerializer.SerializeToElement(string.Empty)),
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

        return Gen.SampledFrom(numbers).Select(static n => JsonSerializer.SerializeToElement(n));
    }

    private static Strategy<JsonElement> BuildMessageFieldStrategy(FieldDescriptor field, int depth)
    {
        MessageDescriptor nestedDescriptor = field.MessageType;

        // Well-known type shortcuts
        string fullName = nestedDescriptor.FullName;
        if (fullName is "google.protobuf.Timestamp" or "google.protobuf.Duration")
        {
            return Gen.Strings().Select(static v => JsonSerializer.SerializeToElement(v));
        }

        if (fullName is "google.protobuf.Any")
        {
            return Gen.Just(JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["@type"] = "type.googleapis.com/google.protobuf.Empty",
            }));
        }

        if (depth <= 0)
        {
            // Return an empty object at max depth to avoid stack overflow.
            return Gen.Just(SerializeObject([]));
        }

        return BuildMessageStrategy(nestedDescriptor, depth - 1);
    }

    private static JsonElement SerializeArray(List<JsonElement> items)
    {
        return JsonSerializer.SerializeToElement(items);
    }

    private static JsonElement SerializeObject(Dictionary<string, JsonElement> obj)
    {
        return JsonSerializer.SerializeToElement(obj);
    }
}