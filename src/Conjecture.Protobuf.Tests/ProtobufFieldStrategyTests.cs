// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text.Json;

using Conjecture.Core;
using Conjecture.Protobuf;

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Conjecture.Protobuf.Tests;

public sealed class ProtobufFieldStrategyTests
{
    // Build a FileDescriptor containing:
    //   message Person { string name = 1; int32 age = 2; repeated string emails = 3; }
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
                        new FieldDescriptorProto
                        {
                            Name = "emails",
                            Number = 3,
                            Type = FieldDescriptorProto.Types.Type.String,
                            Label = FieldDescriptorProto.Types.Label.Repeated,
                            JsonName = "emails",
                        },
                    },
                },
            },
        };
        FileDescriptor file = FileDescriptor.BuildFromByteStrings([proto.ToByteString()])[0];
        return file.MessageTypes[0];
    }

    // Build a FileDescriptor containing:
    //   message Node { Node child = 1; }
    private static MessageDescriptor BuildNodeDescriptor()
    {
        FileDescriptorProto proto = new()
        {
            Name = "node.proto",
            Syntax = "proto3",
            MessageType =
            {
                new DescriptorProto
                {
                    Name = "Node",
                    Field =
                    {
                        new FieldDescriptorProto
                        {
                            Name = "child",
                            Number = 1,
                            Type = FieldDescriptorProto.Types.Type.Message,
                            Label = FieldDescriptorProto.Types.Label.Optional,
                            TypeName = ".Node",
                            JsonName = "child",
                        },
                    },
                },
            },
        };
        FileDescriptor file = FileDescriptor.BuildFromByteStrings([proto.ToByteString()])[0];
        return file.MessageTypes[0];
    }

    // Build a FileDescriptor with a oneof:
    //   message Payload { oneof body { string text = 1; int32 code = 2; } }
    private static MessageDescriptor BuildOneofDescriptor()
    {
        FileDescriptorProto proto = new()
        {
            Name = "payload.proto",
            Syntax = "proto3",
            MessageType =
            {
                new DescriptorProto
                {
                    Name = "Payload",
                    OneofDecl = { new OneofDescriptorProto { Name = "body" } },
                    Field =
                    {
                        new FieldDescriptorProto
                        {
                            Name = "text",
                            Number = 1,
                            Type = FieldDescriptorProto.Types.Type.String,
                            Label = FieldDescriptorProto.Types.Label.Optional,
                            JsonName = "text",
                            OneofIndex = 0,
                        },
                        new FieldDescriptorProto
                        {
                            Name = "code",
                            Number = 2,
                            Type = FieldDescriptorProto.Types.Type.Int32,
                            Label = FieldDescriptorProto.Types.Label.Optional,
                            JsonName = "code",
                            OneofIndex = 0,
                        },
                    },
                },
            },
        };
        FileDescriptor file = FileDescriptor.BuildFromByteStrings([proto.ToByteString()])[0];
        return file.MessageTypes[0];
    }

    [Fact]
    public void Generate_PersonDescriptor_ProducesObjectValueKind()
    {
        MessageDescriptor descriptor = BuildPersonDescriptor();
        ProtobufFieldStrategy strategy = new(descriptor);
        IReadOnlyList<JsonElement> samples = strategy.WithSeed(42UL).Sample(10);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
        }
    }

    [Fact]
    public void Generate_PersonDescriptor_ObjectContainsNameAsString()
    {
        MessageDescriptor descriptor = BuildPersonDescriptor();
        ProtobufFieldStrategy strategy = new(descriptor);
        IReadOnlyList<JsonElement> samples = strategy.WithSeed(42UL).Sample(10);
        foreach (JsonElement element in samples)
        {
            Assert.True(element.TryGetProperty("name", out JsonElement name));
            Assert.Equal(JsonValueKind.String, name.ValueKind);
        }
    }

    [Fact]
    public void Generate_PersonDescriptor_ObjectContainsAgeAsNumber()
    {
        MessageDescriptor descriptor = BuildPersonDescriptor();
        ProtobufFieldStrategy strategy = new(descriptor);
        IReadOnlyList<JsonElement> samples = strategy.WithSeed(42UL).Sample(10);
        foreach (JsonElement element in samples)
        {
            Assert.True(element.TryGetProperty("age", out JsonElement age));
            Assert.Equal(JsonValueKind.Number, age.ValueKind);
        }
    }

    [Fact]
    public void Generate_PersonDescriptor_ObjectContainsEmailsAsArray()
    {
        MessageDescriptor descriptor = BuildPersonDescriptor();
        ProtobufFieldStrategy strategy = new(descriptor);
        IReadOnlyList<JsonElement> samples = strategy.WithSeed(42UL).Sample(10);
        foreach (JsonElement element in samples)
        {
            Assert.True(element.TryGetProperty("emails", out JsonElement emails));
            Assert.Equal(JsonValueKind.Array, emails.ValueKind);
        }
    }

    [Fact]
    public void Generate_PersonDescriptor_EmailsArrayContainsOnlyStrings()
    {
        MessageDescriptor descriptor = BuildPersonDescriptor();
        ProtobufFieldStrategy strategy = new(descriptor);
        IReadOnlyList<JsonElement> samples = strategy.WithSeed(99UL).Sample(20);
        foreach (JsonElement element in samples)
        {
            element.TryGetProperty("emails", out JsonElement emails);
            foreach (JsonElement email in emails.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.String, email.ValueKind);
            }
        }
    }

    [Fact]
    public void Generate_NodeDescriptor_DoesNotThrowWithDefaultMaxDepth()
    {
        MessageDescriptor descriptor = BuildNodeDescriptor();
        ProtobufFieldStrategy strategy = new(descriptor);
        Exception? caught = Record.Exception(() =>
        {
            IReadOnlyList<JsonElement> _ = strategy.WithSeed(1UL).Sample(5);
        });
        Assert.Null(caught);
    }

    [Fact]
    public void Generate_NodeDescriptor_DoesNotThrowWithMaxDepthOne()
    {
        MessageDescriptor descriptor = BuildNodeDescriptor();
        ProtobufFieldStrategy strategy = new(descriptor, maxDepth: 1);
        Exception? caught = Record.Exception(() =>
        {
            IReadOnlyList<JsonElement> _ = strategy.WithSeed(1UL).Sample(5);
        });
        Assert.Null(caught);
    }

    [Fact]
    public void Generate_OneofDescriptor_ProducesObjectValueKind()
    {
        MessageDescriptor descriptor = BuildOneofDescriptor();
        ProtobufFieldStrategy strategy = new(descriptor);
        IReadOnlyList<JsonElement> samples = strategy.WithSeed(7UL).Sample(20);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
        }
    }

    [Fact]
    public void Generate_OneofDescriptor_ExactlyOneOneofFieldPresentPerSample()
    {
        MessageDescriptor descriptor = BuildOneofDescriptor();
        ProtobufFieldStrategy strategy = new(descriptor);
        IReadOnlyList<JsonElement> samples = strategy.WithSeed(7UL).Sample(50);
        foreach (JsonElement element in samples)
        {
            bool hasText = element.TryGetProperty("text", out JsonElement _);
            bool hasCode = element.TryGetProperty("code", out JsonElement _);
            Assert.True(hasText ^ hasCode, "Exactly one oneof arm must be present per generated object.");
        }
    }
}