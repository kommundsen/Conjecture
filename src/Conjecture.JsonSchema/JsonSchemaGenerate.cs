// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.IO;
using System.Text.Json;

using Conjecture.Core;

namespace Conjecture.JsonSchema;

/// <summary>Extension methods on <see cref="Generate"/> for JSON Schema-driven generation.</summary>
public static class JsonSchemaGenerate
{
    extension(Generate)
    {
        /// <summary>Returns a <see cref="Strategy{T}"/> that generates <see cref="JsonElement"/> values conforming to <paramref name="jsonSchemaText"/>.</summary>
        /// <exception cref="JsonException">Thrown at construction time if <paramref name="jsonSchemaText"/> is not valid JSON.</exception>
        public static Strategy<JsonElement> FromJsonSchema(string jsonSchemaText)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonSchemaText);
                JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
                return new JsonSchemaStrategy(node);
            }
            catch (JsonException ex) when (ex.GetType() != typeof(JsonException))
            {
                throw new JsonException(ex.Message, ex);
            }
        }

        /// <summary>Returns a <see cref="Strategy{T}"/> that generates <see cref="JsonElement"/> values conforming to <paramref name="root"/>.</summary>
        public static Strategy<JsonElement> FromJsonSchema(JsonElement root)
        {
            JsonSchemaNode node = JsonSchemaParser.Parse(root);
            return new JsonSchemaStrategy(node);
        }

        /// <summary>Returns a <see cref="Strategy{T}"/> that generates <see cref="JsonElement"/> values conforming to the schema in <paramref name="schemaFile"/>.</summary>
        /// <exception cref="JsonException">Thrown at construction time if the file does not contain valid JSON.</exception>
        public static Strategy<JsonElement> FromJsonSchema(FileInfo schemaFile)
        {
            string text = File.ReadAllText(schemaFile.FullName);
            using JsonDocument doc = JsonDocument.Parse(text);
            JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
            return new JsonSchemaStrategy(node);
        }
    }
}