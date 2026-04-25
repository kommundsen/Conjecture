// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Core;

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Conjecture.Protobuf;

/// <summary>Extension methods on <see cref="Generate"/> for Protobuf-driven generation.</summary>
#pragma warning disable RS0026 // multiple overloads with optional parameters
public static class ProtobufGenerate
{
    extension(Generate)
    {
        /// <summary>Returns a strategy that generates <see cref="JsonElement"/> objects shaped by the Protobuf message <typeparamref name="T"/>.</summary>
        public static Strategy<JsonElement> FromProtobuf<T>(int maxDepth = 5)
            where T : IMessage<T>, new()
        {
            MessageDescriptor descriptor = new T().Descriptor;
            return new ProtobufFieldStrategy(descriptor, maxDepth);
        }

        /// <summary>Returns a strategy that generates <see cref="JsonElement"/> objects shaped by the given <paramref name="descriptor"/>.</summary>
        public static Strategy<JsonElement> FromProtobuf(MessageDescriptor descriptor, int maxDepth = 5)
        {
            return new ProtobufFieldStrategy(descriptor, maxDepth);
        }
    }
}
#pragma warning restore RS0026