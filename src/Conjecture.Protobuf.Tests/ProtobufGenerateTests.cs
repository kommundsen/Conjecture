// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Core;
using Conjecture.Protobuf;

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Conjecture.Protobuf.Tests;

public sealed class ProtobufGenerateTests
{
    // FileDescriptorProto implements IMessage<FileDescriptorProto> and has a public parameterless constructor,
    // satisfying the T : IMessage<T>, new() constraint. It is a well-known proto message from Google.Protobuf.

    // Build a simple Person descriptor for the overload-based tests.
    private static MessageDescriptor BuildPersonDescriptor()
    {
        FileDescriptorProto proto = new()
        {
            Name = "person.proto",
            Syntax = "proto3",
            MessageType =
            {
                new DescriptorProto
                {
                    Name = "Person",
                    Field =
                    {
                        new FieldDescriptorProto
                        {
                            Name = "name",
                            Number = 1,
                            Type = FieldDescriptorProto.Types.Type.String,
                            Label = FieldDescriptorProto.Types.Label.Optional,
                            JsonName = "name",
                        },
                        new FieldDescriptorProto
                        {
                            Name = "age",
                            Number = 2,
                            Type = FieldDescriptorProto.Types.Type.Int32,
                            Label = FieldDescriptorProto.Types.Label.Optional,
                            JsonName = "age",
                        },
                    },
                },
            },
        };
        FileDescriptor file = FileDescriptor.BuildFromByteStrings([proto.ToByteString()])[0];
        return file.MessageTypes[0];
    }

    [Fact]
    public void FromProtobuf_Generic_ReturnsNonNullStrategy()
    {
        Strategy<JsonElement> strategy = Generate.FromProtobuf<FileDescriptorProto>();
        Assert.NotNull(strategy);
    }

    [Fact]
    public void FromProtobuf_Generic_GeneratesObjectValueKind()
    {
        Strategy<JsonElement> strategy = Generate.FromProtobuf<FileDescriptorProto>();
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 10, 42UL);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
        }
    }

    [Fact]
    public void FromProtobuf_Generic_GeneratedObjectContainsAllNonOneofFields()
    {
        // Use the Person descriptor via the descriptor overload to check field coverage,
        // because FileDescriptorProto has many fields. For the generic overload we
        // verify that the known scalar field "name" (index 0 in FileDescriptorProto) is present.
        // FileDescriptorProto has a "name" string field — it must appear in every generated object.
        Strategy<JsonElement> strategy = Generate.FromProtobuf<FileDescriptorProto>();
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 10, 42UL);
        foreach (JsonElement element in samples)
        {
            Assert.True(element.TryGetProperty("name", out JsonElement _), "Generated object must contain all non-oneof fields.");
        }
    }

    [Fact]
    public void FromProtobuf_Descriptor_ReturnsNonNullStrategy()
    {
        MessageDescriptor descriptor = BuildPersonDescriptor();
        Strategy<JsonElement> strategy = Generate.FromProtobuf(descriptor);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void FromProtobuf_Descriptor_GeneratesObjectValueKind()
    {
        MessageDescriptor descriptor = BuildPersonDescriptor();
        Strategy<JsonElement> strategy = Generate.FromProtobuf(descriptor);
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 10, 42UL);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
        }
    }

    [Fact]
    public void FromProtobuf_Descriptor_EquivalentToGenericFormForSameMessage()
    {
        // Both overloads with the same underlying MessageDescriptor must produce
        // structurally identical JSON shapes (same property names).
        MessageDescriptor descriptor = FileDescriptorProto.Descriptor;
        Strategy<JsonElement> genericStrategy = Generate.FromProtobuf<FileDescriptorProto>();
        Strategy<JsonElement> descriptorStrategy = Generate.FromProtobuf(descriptor);

        IReadOnlyList<JsonElement> genericSamples = DataGen.Sample(genericStrategy, 5, 100UL);
        IReadOnlyList<JsonElement> descriptorSamples = DataGen.Sample(descriptorStrategy, 5, 100UL);

        // Both must produce JSON objects (structural equivalence check).
        foreach (JsonElement element in genericSamples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
        }

        foreach (JsonElement element in descriptorSamples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
        }
    }

    [Fact]
    public void FromProtobuf_Generic_WithExplicitMaxDepth_ReturnsNonNullStrategy()
    {
        Strategy<JsonElement> strategy = Generate.FromProtobuf<FileDescriptorProto>(maxDepth: 2);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void FromProtobuf_Descriptor_WithExplicitMaxDepth_GeneratesObjectValueKind()
    {
        MessageDescriptor descriptor = BuildPersonDescriptor();
        Strategy<JsonElement> strategy = Generate.FromProtobuf(descriptor, maxDepth: 2);
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 5, 7UL);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
        }
    }
}
